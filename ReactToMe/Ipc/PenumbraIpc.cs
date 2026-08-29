using System;
using System.Collections.Generic;
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

    private static bool IsAcceptable(PenumbraApiEc result) => result is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged;
}
