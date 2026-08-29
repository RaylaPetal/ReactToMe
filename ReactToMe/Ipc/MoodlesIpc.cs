using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;

namespace ReactToMe.Ipc;

public readonly record struct MoodleInfo(uint IconId, string RawTitle);

public sealed class MoodlesIpc
{
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;
    private readonly EzIPCDisposalToken[] tokens;

    // Delegate field names/signatures must match Moodles' current IPC surface exactly (EzIPC wires
    // by field name against the "Moodles" prefix) — confirmed against kawaii/Moodles' IPCProcessor.cs.
    [EzIPC] private Action<Guid, IPlayerCharacter> AddOrUpdateMoodleByPlayerV2 = null!;
    [EzIPC] private Action<Guid, IPlayerCharacter> RemoveMoodleByPlayerV2 = null!;

    // Operates on individual saved Statuses (not Presets) — matches what Add/RemoveMoodleByPlayerV2 expect.
    [EzIPC] private Func<List<(Guid Id, uint IconId, string FullPath, string Title)>> GetRegisteredMoodlesV2 = null!;

    public MoodlesIpc(IPluginLog log, IChatGui chatGui)
    {
        this.log = log;
        this.chatGui = chatGui;
        tokens = EzIPC.Init(this, "Moodles");
    }

    public void ApplyToLocalPlayer(Guid moodleGuid)
    {
        var player = Player.Object;
        if (player == null)
            return;

        try
        {
            AddOrUpdateMoodleByPlayerV2(moodleGuid, player);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Moodles IPC unavailable while applying moodle {MoodleGuid}", moodleGuid);
            chatGui.PrintError("[ReactToMe] Could not apply Moodle — is Moodles installed and loaded?");
        }
    }

    public void RemoveFromLocalPlayer(Guid moodleGuid)
    {
        var player = Player.Object;
        if (player == null)
            return;

        try
        {
            RemoveMoodleByPlayerV2(moodleGuid, player);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Moodles IPC unavailable while removing moodle {MoodleGuid}", moodleGuid);
            chatGui.PrintError("[ReactToMe] Could not remove Moodle — is Moodles installed and loaded?");
        }
    }

    /// <summary>Moodle status GUID -> (icon id, raw title incl. any [tag] formatting), for populating a moodle picker.</summary>
    public IReadOnlyDictionary<Guid, MoodleInfo> GetMoodles()
    {
        try
        {
            var result = new Dictionary<Guid, MoodleInfo>();
            foreach (var moodle in GetRegisteredMoodlesV2())
                result[moodle.Id] = new MoodleInfo(moodle.IconId, moodle.Title);
            return result;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Moodles IPC unavailable while listing moodles");
            return new Dictionary<Guid, MoodleInfo>();
        }
    }

    public void Dispose()
    {
        foreach (var token in tokens)
            token.Dispose();
    }
}
