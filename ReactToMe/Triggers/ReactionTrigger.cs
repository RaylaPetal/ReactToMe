using System;

namespace ReactToMe.Triggers;

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

    /// <summary>If the same trigger fires again while active, restart the timer instead of ignoring it.</summary>
    public bool RefreshOnRepeat { get; set; } = true;

    /// <summary>Allow multiple active instances of this trigger at once instead of single-slot replace-on-restack.</summary>
    public bool StackMultiple { get; set; } = false;

    public bool HasAnyAction => GlamourerDesignId != Guid.Empty || MoodleGuid != Guid.Empty || !string.IsNullOrWhiteSpace(ChatMessage);
}
