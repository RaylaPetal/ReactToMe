using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace ReactToMe.Ipc;

public sealed class PenumbraIpc
{
    private const int LocalPlayerObjectIndex = 0;

    // Ties every temporary setting ReactToMe creates to itself via Penumbra's ownership "key" mechanism,
    // so only ReactToMe's own calls can later modify or remove them.
    private const int TemporarySettingKey = 0x52544D65; // "RTMe"
    private const string TemporarySettingSource = "ReactToMe";

    private readonly GetCollectionForObject getCollectionForObject;
    private readonly GetModList getModList;
    private readonly GetAvailableModSettings getAvailableModSettings;
    private readonly GetCurrentModSettings getCurrentModSettings;
    private readonly SetTemporaryModSettingsPlayer setTemporaryModSettingsPlayer;
    private readonly RemoveTemporaryModSettingsPlayer removeTemporaryModSettingsPlayer;
    private readonly GetModDirectory getModDirectory;
    private readonly AddMod addMod;
    private readonly ReloadMod reloadMod;
    private readonly DeleteMod deleteMod;
    private readonly SetModPath setModPath;
    private readonly GetPlayerResourcesOfType getPlayerResourcesOfType;
    private readonly ConvertTextureFile convertTextureFile;
    private readonly ConvertTextureData convertTextureData;
    private readonly TrySetModPriority trySetModPriority;
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;

    public PenumbraIpc(IDalamudPluginInterface pluginInterface, IPluginLog log, IChatGui chatGui)
    {
        getCollectionForObject = new GetCollectionForObject(pluginInterface);
        getModList = new GetModList(pluginInterface);
        getAvailableModSettings = new GetAvailableModSettings(pluginInterface);
        getCurrentModSettings = new GetCurrentModSettings(pluginInterface);
        setTemporaryModSettingsPlayer = new SetTemporaryModSettingsPlayer(pluginInterface);
        removeTemporaryModSettingsPlayer = new RemoveTemporaryModSettingsPlayer(pluginInterface);
        getModDirectory = new GetModDirectory(pluginInterface);
        addMod = new AddMod(pluginInterface);
        reloadMod = new ReloadMod(pluginInterface);
        deleteMod = new DeleteMod(pluginInterface);
        setModPath = new SetModPath(pluginInterface);
        getPlayerResourcesOfType = new GetPlayerResourcesOfType(pluginInterface);
        convertTextureFile = new ConvertTextureFile(pluginInterface);
        convertTextureData = new ConvertTextureData(pluginInterface);
        trySetModPriority = new TrySetModPriority(pluginInterface);
        this.log = log;
        this.chatGui = chatGui;
    }

    /// <summary>Mod directory -> display name, for populating a mod picker.</summary>
    public IReadOnlyDictionary<string, string> GetMods()
    {
        try
        {
            return getModList.Invoke();
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while listing mods");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Option group name -> (option names, group type), for populating group/stage pickers.</summary>
    public IReadOnlyDictionary<string, (string[] Options, GroupType Type)> GetModSettings(string modDirectory, string modName)
    {
        try
        {
            var settings = getAvailableModSettings.Invoke(modDirectory, modName);
            var result = new Dictionary<string, (string[] Options, GroupType Type)>();
            if (settings != null)
                foreach (var (groupName, value) in settings)
                    result[groupName] = value;
            return result;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while listing mod settings for {ModDirectory}", modDirectory);
            return new Dictionary<string, (string[] Options, GroupType Type)>();
        }
    }

    /// <summary>Reads the mod's current effective priority (its own setting if any, else whatever it
    /// inherits), so a temporary override never resets a priority the user has deliberately configured.
    /// Falls back to 0 (Penumbra's own default) only if the mod has no resolvable priority at all.</summary>
    private int GetCurrentPriority(string modDirectory, string modName)
    {
        try
        {
            var (objectValid, _, collection) = getCollectionForObject.Invoke(LocalPlayerObjectIndex);
            if (!objectValid)
                return 0;

            var (ec, settings) = getCurrentModSettings.Invoke(collection.Id, modDirectory, modName, ignoreInheritance: false);
            return ec == PenumbraApiEc.Success && settings != null ? settings.Value.Item2 : 0;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while reading current priority for {ModDirectory}", modDirectory);
            return 0;
        }
    }

    /// <summary>Temporarily enables the mod and sets its option group to the given option, scoped to the
    /// local player via Penumbra's temporary-settings IPC — never a write to the mod's permanent
    /// configuration. Used both for the initial stage and every subsequent stage advance.</summary>
    public void SetStage(string modDirectory, string modName, string optionGroupName, string optionName)
    {
        try
        {
            var settings = new Dictionary<string, IReadOnlyList<string>> { [optionGroupName] = [optionName] };
            var result = setTemporaryModSettingsPlayer.Invoke(
                LocalPlayerObjectIndex,
                modDirectory,
                inherit: false, // Penumbra's internal "ForceInherit" — false makes our own enabled/settings below actually apply.
                enabled: true,
                priority: GetCurrentPriority(modDirectory, modName),
                settings: settings,
                source: TemporarySettingSource,
                key: TemporarySettingKey,
                modName: modName);

            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected the temporary stage for {ModDirectory}: {Result}", modDirectory, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not apply \"{modName}\"'s stage ({result}) — check ReactToMe has temporary-settings access to Penumbra, then try again.");
            }
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while setting mod stage for {ModDirectory}", modDirectory);
            chatGui.PrintError("[ReactToMe] Could not set Penumbra mod stage — is Penumbra installed and loaded?");
        }
    }

    /// <summary>Removes ReactToMe's temporary override for the mod, reverting it to whatever its
    /// permanent configuration already was — never a permanent change of its own.</summary>
    public void DisableMod(string modDirectory, string modName)
    {
        try
        {
            var result = removeTemporaryModSettingsPlayer.Invoke(LocalPlayerObjectIndex, modDirectory, TemporarySettingKey, modName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected clearing the temporary stage for {ModDirectory}: {Result}", modDirectory, result);
                chatGui.PrintError($"[ReactToMe] Could not clear \"{modName}\"'s temporary stage ({result}).");
            }
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while disabling mod {ModDirectory}", modDirectory);
            chatGui.PrintError("[ReactToMe] Could not disable Penumbra mod — is Penumbra installed and loaded?");
        }
    }

    /// <summary>Penumbra's own mod root directory, so a generated mod can be written to a real subfolder
    /// of it before being registered via <see cref="AddGeneratedMod"/>. Returns empty on failure.</summary>
    public string GetModDirectory()
    {
        try
        {
            return getModDirectory.Invoke() ?? string.Empty;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while reading the mod directory");
            return string.Empty;
        }
    }

    /// <summary>Registers a mod folder that's already been written to disk under Penumbra's mod directory
    /// — Penumbra itself never creates the files, it only starts tracking a directory that already has
    /// them. Used once per overlay-mod-builder project, the first time it's applied.</summary>
    public bool AddGeneratedMod(string modDirectoryName)
    {
        try
        {
            var result = addMod.Invoke(modDirectoryName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected adding generated mod {ModDirectoryName}: {Result}", modDirectoryName, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not accept the generated mod \"{modDirectoryName}\" ({result}).");
                return false;
            }

            return true;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while adding generated mod {ModDirectoryName}", modDirectoryName);
            chatGui.PrintError("[ReactToMe] Could not register the generated mod — is Penumbra installed and loaded?");
            return false;
        }
    }

    /// <summary>Tells Penumbra to re-read an already-registered mod's files from disk — used after
    /// overwriting a generated mod's option files in place on a re-apply.</summary>
    public bool ReloadGeneratedMod(string modDirectoryName, string modName)
    {
        try
        {
            var result = reloadMod.Invoke(modDirectoryName, modName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected reloading generated mod {ModDirectoryName}: {Result}", modDirectoryName, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not reload the generated mod \"{modName}\" ({result}).");
                return false;
            }

            return true;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while reloading generated mod {ModDirectoryName}", modDirectoryName);
            chatGui.PrintError("[ReactToMe] Could not reload the generated mod — is Penumbra installed and loaded?");
            return false;
        }
    }

    /// <summary>Permanently deletes a mod from Penumbra — its own files/folder included. Used when a
    /// project is removed from ReactToMe so its generated mod doesn't linger in Penumbra's mod list
    /// forever.</summary>
    public bool DeleteGeneratedMod(string modDirectoryName, string modName)
    {
        try
        {
            var result = deleteMod.Invoke(modDirectoryName, modName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected deleting generated mod {ModDirectoryName}: {Result}", modDirectoryName, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not delete the generated mod \"{modName}\" ({result}).");
                return false;
            }

            return true;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while deleting generated mod {ModDirectoryName}", modDirectoryName);
            chatGui.PrintError("[ReactToMe] Could not delete the generated mod — is Penumbra installed and loaded?");
            return false;
        }
    }

    /// <summary>Sets a mod's sort-order path in Penumbra's own mod list (e.g. "Body/My Overlay Project"),
    /// filing it under a folder the same way typing a path in Penumbra's UI would.</summary>
    public bool SetModPath(string modDirectoryName, string newPath, string modName)
    {
        try
        {
            var result = setModPath.Invoke(modDirectoryName, newPath, modName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected setting the mod path for {ModDirectoryName}: {Result}", modDirectoryName, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not accept the folder change for \"{modName}\" ({result}).");
                return false;
            }

            return true;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while setting mod path for {ModDirectoryName}", modDirectoryName);
            chatGui.PrintError("[ReactToMe] Could not set the generated mod's folder — is Penumbra installed and loaded?");
            return false;
        }
    }

    /// <summary>Sets a mod's priority in the local player's active collection — Penumbra's own inter-mod
    /// conflict resolution (which mod wins when two enabled mods redirect the same file), unrelated to
    /// anything written into the mod's own files on disk.</summary>
    public bool SetModPriority(string modDirectory, string modName, int priority)
    {
        try
        {
            var (objectValid, _, collection) = getCollectionForObject.Invoke(LocalPlayerObjectIndex);
            if (!objectValid)
            {
                log.Warning("Overlay mod builder: no active collection for the local player while setting priority for {ModDirectory}", modDirectory);
                chatGui.PrintError($"[ReactToMe] Could not set \"{modName}\"'s priority — no active Penumbra collection for your character.");
                return false;
            }

            var result = trySetModPriority.Invoke(collection.Id, modDirectory, priority, modName);
            if (!IsAcceptable(result))
            {
                log.Warning("Penumbra rejected setting priority for {ModDirectory}: {Result}", modDirectory, result);
                chatGui.PrintError($"[ReactToMe] Penumbra did not accept the priority change for \"{modName}\" ({result}).");
                return false;
            }

            return true;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while setting priority for {ModDirectory}", modDirectory);
            chatGui.PrintError("[ReactToMe] Could not set the generated mod's priority — is Penumbra installed and loaded?");
            return false;
        }
    }

    /// <summary>Every texture whose immediate parent directory is named "chara" (e.g. "chara/bibo_mid_base.tex",
    /// or an absolute path like "Z:\...\Skin Overlay Kaede\chara\kaede overlay.tex" — what precedes "chara"
    /// doesn't matter, only that it directly contains the file), from two combined sources: every texture
    /// Penumbra currently reports as actively resolving for the local player (the original approach, which
    /// depends on the character's current render state — a redirect not presently contributing to what's
    /// on-screen won't show up this way), and every installed mod's own declared file redirect read directly
    /// off its <c>meta.json</c> on disk (independent of render state or whether the mod is even enabled).
    /// Combining both means whichever source actually has a given target still surfaces it. Not filtered by
    /// any naming convention beyond the "chara" structural check — see <see cref="IsDirectCharaPath"/>.</summary>
    public IReadOnlyDictionary<string, string> GetTextureOverlayCandidates()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var resources = getPlayerResourcesOfType.Invoke(ResourceType.Tex, false);
            if (resources.TryGetValue(LocalPlayerObjectIndex, out var playerResources))
                foreach (var (fullPath, _, _) in playerResources.Values)
                    if (IsDirectCharaPath(fullPath))
                        result[fullPath] = fullPath;
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while listing player textures");
        }

        AddModDeclaredCharaFiles(result);

        return result;
    }

    /// <summary>Scans every installed mod's own <c>meta.json</c> (its <c>DefaultData.Files</c> plus every
    /// group/option's own <c>Files</c>) for redirects whose immediate parent directory is "chara", adding
    /// each to <paramref name="result"/>. Mods using separate <c>group_NNN.json</c> files instead of inline
    /// <c>Groups</c> aren't scanned for their per-option files (their <c>DefaultData.Files</c> still is) —
    /// not hit in practice yet; extend here if it ever needs to be. A mod whose <c>meta.json</c> is missing,
    /// unreadable, or a different shape is silently skipped rather than failing the whole scan.</summary>
    private void AddModDeclaredCharaFiles(Dictionary<string, string> result)
    {
        var modRoot = getModDirectory.Invoke();
        if (string.IsNullOrEmpty(modRoot))
            return;

        IReadOnlyDictionary<string, string> mods;
        try
        {
            mods = getModList.Invoke();
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while listing mods for texture overlay candidates");
            return;
        }

        foreach (var modDirectoryName in mods.Keys)
        {
            var modFolder = Path.Combine(modRoot, modDirectoryName);
            var metaPath = Path.Combine(modFolder, "meta.json");
            if (!File.Exists(metaPath))
                continue;

            ModMetaFileDto? meta;
            try
            {
                meta = JsonSerializer.Deserialize<ModMetaFileDto>(File.ReadAllText(metaPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                log.Information(ex, "Overlay mod builder: skipping unreadable meta.json for mod {ModDirectoryName}", modDirectoryName);
                continue;
            }

            if (meta == null)
                continue;

            foreach (var gamePath in meta.DefaultData.Files.Keys)
                AddIfDirectCharaPath(result, modFolder, gamePath, meta.DefaultData.Files[gamePath]);

            foreach (var group in meta.Groups)
            foreach (var option in group.Options)
            foreach (var gamePath in option.Files.Keys)
                AddIfDirectCharaPath(result, modFolder, gamePath, option.Files[gamePath]);
        }
    }

    private static void AddIfDirectCharaPath(Dictionary<string, string> result, string modFolder, string gamePath, string relativeFilePath)
    {
        if (!IsDirectCharaPath(gamePath))
            return;

        result[gamePath] = Path.Combine(modFolder, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class ModMetaFileDto
    {
        public ModMetaDataDto DefaultData { get; set; } = new();
        public List<ModMetaGroupDto> Groups { get; set; } = [];
    }

    private sealed class ModMetaDataDto
    {
        public Dictionary<string, string> Files { get; set; } = new();
    }

    private sealed class ModMetaGroupDto
    {
        public List<ModMetaOptionDto> Options { get; set; } = [];
    }

    private sealed class ModMetaOptionDto
    {
        public Dictionary<string, string> Files { get; set; } = new();
    }

    /// <summary>True when a path's immediate parent directory is named "chara" — e.g. "chara/bibo_mid_base.tex",
    /// or "Z:\...\Skin Overlay Kaede\chara\kaede overlay.tex" (an absolute path some mods report as their
    /// own "game path", using backslashes, with an arbitrary prefix before "chara" — what precedes it
    /// doesn't matter, only that "chara" directly contains the file). False for vanilla's own deeply-nested
    /// "chara/human/.../obj/&lt;part&gt;/..." paths, where the immediate parent is the part name, not "chara"
    /// itself.</summary>
    private static bool IsDirectCharaPath(string gamePath)
    {
        var segments = gamePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[^2].Equals("chara", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Converts raw RGBA32 pixel data into a Penumbra-loadable, uncompressed <c>.tex</c> file at
    /// <paramref name="outputFile"/>, via Penumbra's own texture converter — ReactToMe never implements
    /// its own texture encoder. Returns whether the output file exists afterward, since this IPC call has
    /// no direct success/failure return value of its own.</summary>
    public async Task<bool> BakeTextureAsync(byte[] rgbaData, int width, string outputFile)
    {
        try
        {
            await convertTextureData.Invoke(rgbaData, width, outputFile, TextureType.RgbaTex, mipMaps: true);
            return File.Exists(outputFile);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while converting texture to {OutputFile}", outputFile);
            return false;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to convert texture to {OutputFile}", outputFile);
            return false;
        }
    }

    /// <summary>Converts a texture file on disk (any format Penumbra itself understands, including every
    /// BC-compressed .tex format) into <paramref name="outputFile"/> at the given <paramref name="textureType"/>
    /// — used to decode a base .tex file to a plain PNG via Penumbra's own converter, the same one Penumbra
    /// relies on for its own texture previews, rather than re-implementing BC decompression ourselves.</summary>
    public async Task<bool> ConvertTextureFileAsync(string inputFile, string outputFile, TextureType textureType)
    {
        try
        {
            await convertTextureFile.Invoke(inputFile, outputFile, textureType, mipMaps: false);
            return File.Exists(outputFile);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while converting texture file {InputFile}", inputFile);
            return false;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to convert texture file {InputFile}", inputFile);
            return false;
        }
    }

    private static bool IsAcceptable(PenumbraApiEc result) => result is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
}
