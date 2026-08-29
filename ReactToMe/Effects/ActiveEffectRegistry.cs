using System;
using System.Collections.Generic;
using System.Linq;
using ReactToMe.Ipc;
using ReactToMe.Triggers;

namespace ReactToMe.Effects;

public sealed class ActiveEffect
{
    public required Guid TriggerId { get; init; }

    /// <summary>Null means the effect never auto-reverts (<see cref="ReactionTrigger.NoExpiration"/>).</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>
/// Tracks Glamourer designs applied by triggers and reverts them once their duration elapses.
/// Moodles is not tracked here — its own preset duration handles its own expiry.
/// </summary>
public sealed class ActiveEffectRegistry
{
    private readonly GlamourerIpc glamourerIpc;
    private readonly List<ActiveEffect> activeEffects = [];
    private DateTime lastCheckUtc = DateTime.UtcNow;

    public ActiveEffectRegistry(GlamourerIpc glamourerIpc)
    {
        this.glamourerIpc = glamourerIpc;
    }

    public IReadOnlyList<ActiveEffect> ActiveEffects => activeEffects;

    public void Apply(ReactionTrigger trigger)
    {
        var existing = activeEffects.FirstOrDefault(e => e.TriggerId == trigger.Id);

        if (existing != null)
        {
            if (!trigger.StackMultiple)
            {
                if (trigger.RefreshOnRepeat)
                    existing.ExpiresAtUtc = trigger.NoExpiration ? null : DateTime.UtcNow + trigger.Duration;
                return;
            }
        }
        else if (!trigger.StackMultiple && activeEffects.Count > 0)
        {
            // Replace-on-restack: only one non-stacking effect slot for v1.
            RevertAll();
        }

        glamourerIpc.ApplyDesignToLocalPlayer(trigger.GlamourerDesignId);
        activeEffects.Add(new ActiveEffect
        {
            TriggerId = trigger.Id,
            ExpiresAtUtc = trigger.NoExpiration ? null : DateTime.UtcNow + trigger.Duration,
        });
    }

    /// <summary>Call from a coarse (~1s) tick, not every frame.</summary>
    public void Tick()
    {
        var now = DateTime.UtcNow;
        if (now - lastCheckUtc < TimeSpan.FromSeconds(1))
            return;
        lastCheckUtc = now;

        var expired = activeEffects.Where(e => e.ExpiresAtUtc is { } expiresAt && expiresAt <= now).ToList();
        foreach (var effect in expired)
        {
            glamourerIpc.RevertLocalPlayer();
            activeEffects.Remove(effect);
        }
    }

    public void RevertAll()
    {
        if (activeEffects.Count == 0)
            return;

        glamourerIpc.RevertLocalPlayer();
        activeEffects.Clear();
    }
}
