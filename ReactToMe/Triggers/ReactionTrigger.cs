using System;
using System.Collections.Generic;

namespace ReactToMe.Triggers;

/// <summary>What Glamourer state a trigger's timer expiry reverts to.</summary>
public enum GlamourerRevertMode
{
    Automation,
    SpecificDesign,
}

/// <summary>What fires a trigger. Only the fields belonging to the selected type are used for
/// matching — fields belonging to the others are ignored regardless of their stored value.</summary>
public enum TriggerSourceType
{
    Emote,
    ChatPhrase,
    JobSkill,
}

/// <summary>Who must have said a chat-phrase trigger's <see cref="ReactionTrigger.ChatPhrase"/> for it
/// to fire. Chat messages have no in-game "target" the way an emote or a cast does, so this is its own
/// enum rather than a reuse of <see cref="TriggerScope"/>.</summary>
public enum ChatTriggerSource
{
    AnyoneNearby,
    SelfTyped,
}

/// <summary>How a trigger's Penumbra reaction is configured. <see cref="None"/> is the zero-value so
/// pre-existing configs (saved before this enum existed) deserialize to it; a trigger with
/// <see cref="ReactionTrigger.PenumbraStages"/> already populated and this still at <see cref="None"/> is
/// resolved as <see cref="Staged"/> for backward compatibility rather than trusting the stored zero-value
/// (see <see cref="ReactionTrigger.GetEffectivePenumbraMode"/>).</summary>
public enum PenumbraReactionMode
{
    None,
    Single,
    Staged,
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

    /// <summary>Optional display name shown in the trigger list. Falls back to a label generated from
    /// <see cref="TriggerSourceType"/> and its configuration when empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What fires this trigger. Only that type's fields below are used for matching.</summary>
    public TriggerSourceType TriggerSourceType { get; set; } = TriggerSourceType.Emote;

    // --- Source (only the fields for the selected TriggerSourceType are used) ---

    /// <summary>
    /// Lumina Emote sheet RowId, which is also the game's native EmoteController.EmoteId.
    /// 0 = unset. Picked from a dropdown in the UI rather than typed, so there's no text-matching
    /// against localized/chat-log text at all. Used when <see cref="TriggerSourceType"/> is
    /// <see cref="Triggers.TriggerSourceType.Emote"/>.
    /// </summary>
    public uint EmoteId { get; set; } = 0;

    /// <summary>Who must have performed/been targeted by the emote or job-skill cast for this trigger to
    /// fire. Used for <see cref="Triggers.TriggerSourceType.Emote"/> and
    /// <see cref="Triggers.TriggerSourceType.JobSkill"/>, both of which have a real in-game target.</summary>
    public TriggerScope Scope { get; set; } = TriggerScope.OthersTargetingMe;

    /// <summary>Which side of the local player the source must be standing on, relative to the local
    /// player's facing, for this trigger to match. Only used when <see cref="TriggerSourceType"/> is
    /// <see cref="Triggers.TriggerSourceType.Emote"/> and <see cref="Scope"/> is
    /// <see cref="Triggers.TriggerScope.OthersTargetingMe"/> — ignored for every other combination, the
    /// same way <see cref="ChatPhrase"/>/<see cref="JobSkillActionId"/> are ignored outside their own
    /// <see cref="TriggerSourceType"/>. <see cref="Triggers.DirectionFilter.Any"/> is the zero-value
    /// default so pre-existing configs (saved before this field existed) deserialize into the old,
    /// direction-agnostic behavior.</summary>
    public DirectionFilter DirectionFilter { get; set; } = DirectionFilter.Any;

    /// <summary>Free-form substring matched case-insensitively against incoming chat messages. No fixed
    /// required prefix or suffix. Used when <see cref="TriggerSourceType"/> is
    /// <see cref="Triggers.TriggerSourceType.ChatPhrase"/>.</summary>
    public string ChatPhrase { get; set; } = string.Empty;

    /// <summary>Who must have said <see cref="ChatPhrase"/> for this trigger to fire.</summary>
    public ChatTriggerSource ChatTriggerSource { get; set; } = ChatTriggerSource.AnyoneNearby;

    /// <summary>Optional case-insensitive character-name filter applied on top of <see cref="Scope"/> (for
    /// <see cref="Triggers.TriggerSourceType.Emote"/>/<see cref="Triggers.TriggerSourceType.JobSkill"/>) or
    /// <see cref="ChatTriggerSource"/> (for <see cref="Triggers.TriggerSourceType.ChatPhrase"/>) — restricts
    /// matching to one specific character by name. Has no effect when the existing scope/source setting
    /// already restricts matching to only the local player, since there is no other character to filter
    /// among in that case. Empty (the default) matches exactly as if this field didn't exist.</summary>
    public string CharacterNameFilter { get; set; } = string.Empty;

    /// <summary>Lumina ClassJob sheet RowId of the job the skill picker below is narrowed to. UI
    /// convenience only — matching is done purely on <see cref="JobSkillActionId"/>, not this.</summary>
    public uint JobSkillClassJobId { get; set; } = 0;

    /// <summary>Lumina Action sheet RowId of the job skill to watch for. Used when
    /// <see cref="TriggerSourceType"/> is <see cref="Triggers.TriggerSourceType.JobSkill"/>, matched
    /// against <see cref="Scope"/> for who must be casting it. Only actions with a nonzero cast time are
    /// detectable — instant weaponskills/abilities never populate a detectable cast state.</summary>
    public uint JobSkillActionId { get; set; } = 0;

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

    /// <summary>Whether this trigger's Penumbra reaction is off, a single mod/option applied on first
    /// fire, or a fire-count-staged escalation. Defaults to <see cref="Triggers.PenumbraReactionMode.None"/>,
    /// the enum's zero-value, so old configs deserialize into it; use
    /// <see cref="GetEffectivePenumbraMode"/> rather than reading this directly, since it corrects the
    /// ambiguous old-config case.</summary>
    public PenumbraReactionMode PenumbraReactionMode { get; set; } = PenumbraReactionMode.None;

    /// <summary>Fire-count thresholds, each naming its own Penumbra mod/group/option. The active stage at
    /// any fire count is the entry with the greatest <see cref="PenumbraStageThreshold.Threshold"/> not
    /// exceeding it; if none qualify yet, no stage is applied. Order in this list doesn't matter —
    /// resolution always scans for the greatest qualifying threshold. Empty = no staged mod configured.
    /// In <see cref="Triggers.PenumbraReactionMode.Single"/> mode this holds exactly one entry whose
    /// <see cref="PenumbraStageThreshold.Threshold"/> is always <c>1</c>.</summary>
    public List<PenumbraStageThreshold> PenumbraStages { get; set; } = [];

    /// <summary>Resolves <see cref="PenumbraReactionMode"/> for use, correcting the one ambiguous case: a
    /// config saved before this mode existed has <see cref="PenumbraReactionMode"/> at its zero-value
    /// (<see cref="Triggers.PenumbraReactionMode.None"/>) but may already have stages configured. Such a
    /// trigger is resolved as <see cref="Triggers.PenumbraReactionMode.Staged"/> so its pre-existing
    /// behavior and presentation are unchanged; any other stored value is trusted as-is.</summary>
    public PenumbraReactionMode GetEffectivePenumbraMode() =>
        PenumbraReactionMode == PenumbraReactionMode.None && PenumbraStages.Count > 0
            ? PenumbraReactionMode.Staged
            : PenumbraReactionMode;

    /// <summary>Emote for the local player to perform when this trigger fires, independent of its other
    /// reactions. Fire-and-forget: performed once per fire, not tracked for reverting. 0 = disabled.</summary>
    public uint GestureEmoteId { get; set; } = 0;

    /// <summary>When true and <see cref="GestureEmoteId"/> is set, the local player's current target is
    /// cleared immediately before the gesture command is sent and restored immediately after, so the
    /// game's native "auto-face current target" behavior doesn't rotate the local player out of position
    /// while performing the gesture. Only meaningful when <see cref="GestureEmoteId"/> is nonzero.
    /// Defaults to false, matching pre-existing behavior for triggers saved before this field existed.</summary>
    public bool KeepFacingOnGesture { get; set; } = false;

    public bool HasAnyAction => GlamourerDesignId != Guid.Empty || MoodleGuid != Guid.Empty
        || !string.IsNullOrWhiteSpace(ChatMessage) || PenumbraStages.Count > 0 || GestureEmoteId != 0;
}
