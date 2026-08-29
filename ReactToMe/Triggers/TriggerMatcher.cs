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
        bool targetIsLocalPlayer)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.Emote &&
            t.EmoteId != 0 &&
            t.EmoteId == emoteId &&
            MatchesScope(t.Scope, sourceIsLocalPlayer, targetIsLocalPlayer));
    }

    public static ReactionTrigger? FindJobSkillMatch(
        IEnumerable<ReactionTrigger> triggers,
        uint actionId,
        bool sourceIsLocalPlayer,
        bool targetIsLocalPlayer)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.JobSkill &&
            t.JobSkillActionId != 0 &&
            t.JobSkillActionId == actionId &&
            MatchesScope(t.Scope, sourceIsLocalPlayer, targetIsLocalPlayer));
    }

    public static ReactionTrigger? FindChatPhraseMatch(
        IEnumerable<ReactionTrigger> triggers,
        string messageText,
        bool senderIsLocalPlayer)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.TriggerSourceType == TriggerSourceType.ChatPhrase &&
            !string.IsNullOrWhiteSpace(t.ChatPhrase) &&
            (t.ChatTriggerSource == ChatTriggerSource.AnyoneNearby || senderIsLocalPlayer) &&
            messageText.Contains(t.ChatPhrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesScope(TriggerScope scope, bool sourceIsLocalPlayer, bool targetIsLocalPlayer) => scope switch
    {
        TriggerScope.OthersTargetingMe => !sourceIsLocalPlayer && targetIsLocalPlayer,
        TriggerScope.SelfPerformed => sourceIsLocalPlayer,
        TriggerScope.Anyone => true,
        _ => false,
    };
}
