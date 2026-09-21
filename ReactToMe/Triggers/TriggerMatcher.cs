using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactToMe.Triggers;

public static class TriggerMatcher
{
    public static ReactionTrigger? FindEmoteMatch(
        IEnumerable<ReactionTrigger> triggers,
        uint emoteId,
        bool sourceIsLocalPlayer,
        bool targetIsLocalPlayer,
        string? sourceName,
        DirectionMatch? directionMatch)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.Emote &&
            t.EmoteId != 0 &&
            t.EmoteId == emoteId &&
            MatchesScope(t.Scope, sourceIsLocalPlayer, targetIsLocalPlayer) &&
            MatchesCharacterFilter(t.CharacterNameFilter, sourceName) &&
            MatchesDirection(t.DirectionFilter, directionMatch));
    }

    public static ReactionTrigger? FindJobSkillMatch(
        IEnumerable<ReactionTrigger> triggers,
        uint actionId,
        bool sourceIsLocalPlayer,
        bool targetIsLocalPlayer,
        string? sourceName)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.JobSkill &&
            t.JobSkillActionId != 0 &&
            t.JobSkillActionId == actionId &&
            MatchesScope(t.Scope, sourceIsLocalPlayer, targetIsLocalPlayer) &&
            MatchesCharacterFilter(t.CharacterNameFilter, sourceName));
    }

    public static ReactionTrigger? FindChatPhraseMatch(
        IEnumerable<ReactionTrigger> triggers,
        string messageText,
        bool senderIsLocalPlayer,
        string senderName)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.ChatPhrase &&
            !string.IsNullOrWhiteSpace(t.ChatPhrase) &&
            (t.ChatTriggerSource == ChatTriggerSource.AnyoneNearby || senderIsLocalPlayer) &&
            messageText.Contains(t.ChatPhrase, StringComparison.OrdinalIgnoreCase) &&
            MatchesCharacterFilter(t.CharacterNameFilter, senderName));
    }

    private static bool MatchesScope(TriggerScope scope, bool sourceIsLocalPlayer, bool targetIsLocalPlayer) => scope switch
    {
        TriggerScope.OthersTargetingMe => !sourceIsLocalPlayer && targetIsLocalPlayer,
        TriggerScope.SelfPerformed => sourceIsLocalPlayer,
        TriggerScope.Anyone => true,
        _ => false,
    };

    /// <summary>An empty filter always matches (unchanged behavior). A non-empty filter requires a resolved
    /// source/sender name that contains it, case-insensitively — matching <see cref="ChatMessageListener"/>'s
    /// own "Contains", not an exact-equals, since a chat sender's display text can carry extra formatting
    /// (e.g. a world-name suffix for a cross-world sender).</summary>
    private static bool MatchesCharacterFilter(string filter, string? actualName) =>
        string.IsNullOrEmpty(filter) || (actualName != null && actualName.Contains(filter, StringComparison.OrdinalIgnoreCase));

    /// <summary><see cref="DirectionFilter.Any"/> always matches, unchanged from before this field existed.
    /// A specific requirement (<see cref="DirectionFilter.Behind"/>/<see cref="DirectionFilter.InFront"/>)
    /// fails closed when <paramref name="actualDirection"/> is null — i.e. when the local player's
    /// position/facing couldn't be resolved at the moment of the emote — rather than matching anyway.</summary>
    private static bool MatchesDirection(DirectionFilter filter, DirectionMatch? actualDirection) => filter switch
    {
        DirectionFilter.Any => true,
        DirectionFilter.Behind => actualDirection == DirectionMatch.Behind,
        DirectionFilter.InFront => actualDirection == DirectionMatch.InFront,
        _ => false,
    };
}
