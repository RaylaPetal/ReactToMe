using System.Collections.Generic;
using System.Linq;

namespace ReactToMe.Triggers;

public static class TriggerMatcher
{
    public static ReactionTrigger? FindMatch(
        IEnumerable<ReactionTrigger> triggers,
        uint emoteId,
        bool sourceIsLocalPlayer,
        bool targetIsLocalPlayer)
    {
        return triggers.FirstOrDefault(t =>
            t.IsEnabled &&
            t.HasAnyAction &&
            t.EmoteId != 0 &&
            t.EmoteId == emoteId &&
            MatchesScope(t.Scope, sourceIsLocalPlayer, targetIsLocalPlayer));
    }

    private static bool MatchesScope(TriggerScope scope, bool sourceIsLocalPlayer, bool targetIsLocalPlayer) => scope switch
    {
        TriggerScope.OthersTargetingMe => !sourceIsLocalPlayer && targetIsLocalPlayer,
        TriggerScope.SelfPerformed => sourceIsLocalPlayer,
        TriggerScope.Anyone => true,
        _ => false,
    };
}
