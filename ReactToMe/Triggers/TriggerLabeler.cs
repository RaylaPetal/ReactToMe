using System.Collections.Generic;
using ReactToMe.JobSkills;

namespace ReactToMe.Triggers;

/// <summary>Computes a trigger's display label: its configured <see cref="ReactionTrigger.Name"/> if set,
/// else a label generated from its source type and configuration. Shared between the config window's
/// trigger list and its active-effects list, so both identify a trigger the same way regardless of its
/// source type.</summary>
public static class TriggerLabeler
{
    public static string GetLabel(ReactionTrigger trigger, IReadOnlyDictionary<uint, string> emotes, JobSkillCatalog jobSkillCatalog)
    {
        if (!string.IsNullOrWhiteSpace(trigger.Name))
            return trigger.Name;

        return trigger.TriggerSourceType switch
        {
            TriggerSourceType.Emote => emotes.TryGetValue(trigger.EmoteId, out var emoteName) ? emoteName : "(no emote selected)",
            TriggerSourceType.ChatPhrase => string.IsNullOrWhiteSpace(trigger.ChatPhrase) ? "(no phrase set)" : $"\"{trigger.ChatPhrase}\"",
            TriggerSourceType.JobSkill => jobSkillCatalog.TryGetActionName(trigger.JobSkillActionId, out var skillName) ? skillName : "(no skill selected)",
            _ => "(unknown)",
        };
    }
}
