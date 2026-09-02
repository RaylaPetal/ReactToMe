using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace ReactToMe.JobSkills;

public sealed class JobSkillCastEventArgs : EventArgs
{
    public required uint ActionId { get; init; }
    public required ulong SourceGameObjectId { get; init; }
    public required ulong TargetGameObjectId { get; init; }

    /// <summary>The casting character's name, for <see cref="Triggers.ReactionTrigger.CharacterNameFilter"/>
    /// matching. Null if the actor no longer resolves by the time this is read (rare — it was just polled
    /// this same frame).</summary>
    public string? SourceName { get; init; }
}

/// <summary>
/// Detects job-skill casts by reading each nearby player's native CastInfo.ActionId/IsCasting directly
/// off their Character struct every frame (same technique as ReactToMe.Emotes.EmotePoller), instead of a
/// hook. Works for both self and others since the game populates CastInfo for any nearby character with
/// a visible cast bar. Only detects cast-time actions — instant actions (most weaponskills/abilities)
/// never populate CastInfo and are out of scope.
/// </summary>
public sealed class JobSkillPoller
{
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;
    private readonly Dictionary<ulong, bool> wasCastingByActor = [];

    public event EventHandler<JobSkillCastEventArgs>? JobSkillCast;

    public JobSkillPoller(IObjectTable objectTable, IPluginLog log)
    {
        this.objectTable = objectTable;
        this.log = log;
    }

    public unsafe void Poll()
    {
        foreach (var obj in objectTable)
        {
            if (obj is not IPlayerCharacter playerCharacter)
                continue;

            var native = (Character*)playerCharacter.Address;
            if (native == null)
                continue;

            var castInfo = native->GetCastInfo();
            if (castInfo == null)
                continue;

            var actorId = playerCharacter.GameObjectId;
            var isCasting = castInfo->IsCasting;

            wasCastingByActor.TryGetValue(actorId, out var wasCasting);
            wasCastingByActor[actorId] = isCasting;

            // Fire only on the false -> true transition (start of cast), not every frame while casting.
            if (!isCasting || wasCasting)
                continue;

            var actionId = castInfo->ActionId;
            var targetId = (ulong)castInfo->TargetId;

            log.Information("[ReactToMe] job skill cast polled: actionId={ActionId} source={Source} target={Target}",
                actionId, actorId, targetId);

            JobSkillCast?.Invoke(this, new JobSkillCastEventArgs
            {
                ActionId = actionId,
                SourceGameObjectId = actorId,
                TargetGameObjectId = targetId,
                SourceName = playerCharacter.Name.TextValue,
            });
        }
    }
}
