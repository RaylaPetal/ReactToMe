namespace ReactToMe.Triggers;

public enum TriggerScope
{
    /// <summary>Someone else performs the emote and targets you specifically.</summary>
    OthersTargetingMe,

    /// <summary>You perform the emote yourself, regardless of who (if anyone) it's targeted at.</summary>
    SelfPerformed,

    /// <summary>Any matching emote text seen nearby, regardless of who performed or was targeted by it.</summary>
    Anyone,
}

/// <summary>Which side of the local player the trigger's emote source must be standing on, relative to
/// the local player's facing, for the trigger to match. Only meaningful for
/// <see cref="TriggerSourceType.Emote"/> triggers with <see cref="ReactionTrigger.Scope"/> set to
/// <see cref="TriggerScope.OthersTargetingMe"/> — every other combination has no local-player-relative
/// "other party" to measure against, so this field is ignored there. <see cref="Any"/> is the zero-value
/// default so pre-existing configs (saved before this field existed) deserialize into the old, direction-
/// agnostic behavior.</summary>
public enum DirectionFilter
{
    /// <summary>Matches regardless of relative direction — the original, direction-agnostic behavior.</summary>
    Any,

    /// <summary>Source must be within the 120°-wide arc centered directly behind the local player. See
    /// <see cref="DirectionClassifier"/> for the exact classification.</summary>
    Behind,

    /// <summary>Source must be within the 120°-wide arc centered directly in front of the local player. See
    /// <see cref="DirectionClassifier"/> for the exact classification.</summary>
    InFront,
}
