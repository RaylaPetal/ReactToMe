using System;
using System.Collections.Generic;

namespace ReactToMe.Triggers;

/// <summary>What Glamourer state a trigger's timer expiry reverts to.</summary>
public enum GlamourerRevertMode
{
    Automation,
    SpecificDesign,
}

/// <summary>
/// A fully self-contained Penumbra target — mod, option group, and option — paired with the fire-count
/// threshold at which it becomes active. Each stage independently names its own mod, so the same trigger
/// can escalate within one mod's option group, across different option groups of one mod, or across
/// entirely different mods, all with the same mechanism.
/// </summary>
[Serializable]
public class PenumbraStageThreshold
{
    public int Threshold { get; set; } = 1;

    /// <summary>Directory (folder) name of the Penumbra mod this stage targets.</summary>
    public string ModDirectory { get; set; } = string.Empty;

    /// <summary>Display name of the mod, paired with <see cref="ModDirectory"/> as Penumbra's IPC
    /// identifies a mod by both.</summary>
    public string ModName { get; set; } = string.Empty;

    public string OptionGroupName { get; set; } = string.Empty;
    public string OptionName { get; set; } = string.Empty;
}

[Serializable]
public class ReactionTrigger
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Lumina Emote sheet RowId, which is also the game's native EmoteController.EmoteId.
    /// 0 = unset. Picked from a dropdown in the UI rather than typed, so there's no text-matching
    /// against localized/chat-log text at all.
    /// </summary>
    public uint EmoteId { get; set; } = 0;

    public TriggerScope Scope { get; set; } = TriggerScope.OthersTargetingMe;

    // --- Actions (independently optional; at least one required for the trigger to do anything) ---

    public Guid GlamourerDesignId { get; set; } = Guid.Empty;

    /// <summary>
    /// Moodle GUID to apply. Its own built-in duration governs when it expires
    /// (Moodles reverts itself; this plugin only manages the Glamourer revert timer).
    /// </summary>
    public Guid MoodleGuid { get; set; } = Guid.Empty;

    /// <summary>Chat text/command to send verbatim, e.g. "/s hello!" or "/p party message".
    /// Empty = disabled. Sent via the game's own chatbox submission, so leading "/" runs it as
    /// a real command/channel switch exactly as if typed.</summary>
    public string ChatMessage { get; set; } = string.Empty;

    /// <summary>Minimum seconds between chat sends for this trigger, to avoid tripping the game's
    /// chat spam throttle or flooding a public channel if the condition repeats rapidly.</summary>
    public int ChatCooldownSeconds { get; set; } = 10;

    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Manual mirror of a permanent (no-expiration) Moodle: ReactToMe cannot read a Moodle's own
    /// duration/expiration via IPC (Moodles exposes no safe cross-plugin way to read it), so this is
    /// set by hand to match what the user separately configured in Moodles. Only meaningful when
    /// <see cref="MoodleGuid"/> is set. When true, <see cref="Duration"/> is ignored and applied effects
    /// are never auto-reverted.
    /// </summary>
    public bool NoExpiration { get; set; } = false;

    /// <summary>What Glamourer state this trigger's timer expiry reverts to. Defaults to automation,
    /// matching behavior from before this option existed.</summary>
    public GlamourerRevertMode GlamourerRevertMode { get; set; } = GlamourerRevertMode.Automation;

    /// <summary>Design to apply on expiry when <see cref="GlamourerRevertMode"/> is
    /// <see cref="Triggers.GlamourerRevertMode.SpecificDesign"/>. Falls back to reverting to
    /// automation if left unset (<see cref="Guid.Empty"/>).</summary>
    public Guid RevertToDesignId { get; set; } = Guid.Empty;

    /// <summary>If the same trigger fires again while active, restart the timer instead of ignoring it.</summary>
    public bool RefreshOnRepeat { get; set; } = true;

    /// <summary>Allow multiple active instances of this trigger at once instead of single-slot replace-on-restack.</summary>
    public bool StackMultiple { get; set; } = false;

    /// <summary>Fire-count thresholds, each naming its own Penumbra mod/group/option. The active stage at
    /// any fire count is the entry with the greatest <see cref="PenumbraStageThreshold.Threshold"/> not
    /// exceeding it; if none qualify yet, no stage is applied. Order in this list doesn't matter —
    /// resolution always scans for the greatest qualifying threshold. Empty = no staged mod configured.</summary>
    public List<PenumbraStageThreshold> PenumbraStages { get; set; } = [];

    public bool HasAnyAction => GlamourerDesignId != Guid.Empty || MoodleGuid != Guid.Empty
        || !string.IsNullOrWhiteSpace(ChatMessage) || PenumbraStages.Count > 0;
}
