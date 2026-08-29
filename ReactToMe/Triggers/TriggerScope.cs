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
