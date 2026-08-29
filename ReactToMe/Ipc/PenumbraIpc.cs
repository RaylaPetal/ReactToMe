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

    private readonly GetCollectionForObject getCollectionForObject;
    private readonly GetModList getModList;
    private readonly GetAvailableModSettings getAvailableModSettings;
    private readonly TrySetMod trySetMod;
    private readonly TrySetModSetting trySetModSetting;
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;

    public PenumbraIpc(IDalamudPluginInterface pluginInterface, IPluginLog log, IChatGui chatGui)
    {
        getCollectionForObject = new GetCollectionForObject(pluginInterface);
        getModList = new GetModList(pluginInterface);
        getAvailableModSettings = new GetAvailableModSettings(pluginInterface);
        trySetMod = new TrySetMod(pluginInterface);
        trySetModSetting = new TrySetModSetting(pluginInterface);
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

    /// <summary>Enables the mod (in the local player's active collection) and sets its option group to the
    /// given option, in one call. Used both for the initial stage and every subsequent stage advance.</summary>
    public void SetStage(string modDirectory, string modName, string optionGroupName, string optionName)
    {
        try
        {
            var (objectValid, _, collection) = getCollectionForObject.Invoke(LocalPlayerObjectIndex);
            if (!objectValid)
                return;

            // Penumbra.Api's TrySetMod wrapper names this parameter "inherit" (a copy-paste artifact from
            // TryInheritMod in its own source), but positionally it is the mod's enabled/disabled flag.
            trySetMod.Invoke(collection.Id, modDirectory, true, modName);
            trySetModSetting.Invoke(collection.Id, modDirectory, optionGroupName, optionName, modName);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while setting mod stage for {ModDirectory}", modDirectory);
            chatGui.PrintError("[ReactToMe] Could not set Penumbra mod stage — is Penumbra installed and loaded?");
        }
    }

    /// <summary>Disables the mod in the local player's active collection.</summary>
    public void DisableMod(string modDirectory, string modName)
    {
        try
        {
            var (objectValid, _, collection) = getCollectionForObject.Invoke(LocalPlayerObjectIndex);
            if (!objectValid)
                return;

            trySetMod.Invoke(collection.Id, modDirectory, false, modName);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Penumbra IPC unavailable while disabling mod {ModDirectory}", modDirectory);
            chatGui.PrintError("[ReactToMe] Could not disable Penumbra mod — is Penumbra installed and loaded?");
        }
    }
}
