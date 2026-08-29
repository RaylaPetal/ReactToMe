using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Glamourer.Api.IpcSubscribers;

namespace ReactToMe.Ipc;

public sealed class GlamourerIpc
{
    private const int LocalPlayerObjectIndex = 0;

    private readonly ApplyDesign applyDesign;
    private readonly RevertToAutomation revertToAutomation;
    private readonly GetDesignList getDesignList;
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;

    public GlamourerIpc(IDalamudPluginInterface pluginInterface, IPluginLog log, IChatGui chatGui)
    {
        applyDesign = new ApplyDesign(pluginInterface);
        revertToAutomation = new RevertToAutomation(pluginInterface);
        getDesignList = new GetDesignList(pluginInterface);
        this.log = log;
        this.chatGui = chatGui;
    }

    /// <summary>Design GUID -> display name, for populating a design picker.</summary>
    public IReadOnlyDictionary<Guid, string> GetDesigns()
    {
        try
        {
            return getDesignList.Invoke();
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Glamourer IPC unavailable while listing designs");
            return new Dictionary<Guid, string>();
        }
    }

    public void ApplyDesignToLocalPlayer(Guid designId)
    {
        try
        {
            applyDesign.Invoke(designId, LocalPlayerObjectIndex);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Glamourer IPC unavailable while applying design {DesignId}", designId);
            chatGui.PrintError("[ReactToMe] Could not apply Glamourer design — is Glamourer installed and loaded?");
        }
    }

    public void RevertLocalPlayer()
    {
        try
        {
            revertToAutomation.Invoke(LocalPlayerObjectIndex);
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Glamourer IPC unavailable while reverting local player");
            chatGui.PrintError("[ReactToMe] Could not revert Glamourer design — is Glamourer installed and loaded?");
        }
    }
}
