using Dalamud.Configuration;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using ReactToMe.OverlayModBuilder;
using ReactToMe.Triggers;

namespace ReactToMe;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;

    public List<ReactionTrigger> Triggers { get; set; } = [];

    public List<OverlayModBuilderProject> OverlayModBuilderProjects { get; set; } = [];

    /// <summary>
    /// If true, an active Glamourer effect is reverted immediately on logout instead of
    /// resuming/expiring on wall-clock time after the next login (doc §5's relog question;
    /// v1 defaults to the simpler "implicit revert on logout" behavior).
    /// </summary>
    public bool RevertOnRelog { get; set; } = true;

    /// <summary>Master switch: when false, no trigger matches or fires, regardless of its own
    /// <see cref="ReactionTrigger.IsEnabled"/> state. A fast way to go quiet (e.g. for a call or stream)
    /// without losing any trigger's own configuration — turning this back on restores matching exactly as
    /// each trigger was already configured.</summary>
    public bool ReactionsEnabled { get; set; } = true;

    /// <summary>Chat channel types watched for chat-phrase trigger matching. Defaults to exactly the set
    /// <see cref="ChatDetection.ChatMessageListener"/> used to hardcode, so a config saved before this field
    /// existed (and so deserializes to this default) behaves identically to before.</summary>
    public List<XivChatType> WatchedChatChannels { get; set; } =
    [
        XivChatType.Say,
        XivChatType.Yell,
        XivChatType.Shout,
        XivChatType.TellIncoming,
        XivChatType.TellOutgoing,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
    ];

    /// <summary>Seed value for a newly-created trigger's <see cref="ReactionTrigger.Duration"/>. Changing
    /// this never alters any already-existing trigger's own configured duration.</summary>
    public TimeSpan DefaultTriggerDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Seed value for a newly-created trigger's <see cref="ReactionTrigger.ChatCooldownSeconds"/>.
    /// Changing this never alters any already-existing trigger's own configured cooldown.</summary>
    public int DefaultChatCooldownSeconds { get; set; } = 10;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
