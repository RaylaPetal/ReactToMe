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

    /// <summary>Captured from the trigger at apply time, like <see cref="ExpiresAtUtc"/>.</summary>
    public required GlamourerRevertMode RevertMode { get; set; }

    public required Guid RevertToDesignId { get; set; }

    /// <summary>How many times this trigger has fired, capped at <see cref="ActiveEffectRegistry.MaxPenumbraFireCount"/>
    /// (FFXIV's native maximum debuff stack count). Drives which <see cref="ReactionTrigger.PenumbraStages"/>
    /// threshold is currently active.</summary>
    public int FireCount { get; set; } = 1;

    /// <summary>The mod/group/option last actually applied via Penumbra for the currently active stage, so
    /// repeat fires that don't cross into a new stage don't re-issue IPC calls, and so expiry knows exactly
    /// which mod to disable. Empty means no stage has activated yet.</summary>
    public string PenumbraCurrentModDirectory { get; set; } = string.Empty;

    public string PenumbraCurrentModName { get; set; } = string.Empty;
    public string PenumbraCurrentOptionGroupName { get; set; } = string.Empty;
    public string PenumbraCurrentOptionName { get; set; } = string.Empty;
}

/// <summary>
/// Tracks Glamourer designs (and staged Penumbra mods) applied by triggers and reverts them once their
/// duration elapses. Moodles is not tracked here — its own preset duration handles its own expiry.
/// </summary>
public sealed class ActiveEffectRegistry
{
    /// <summary>FFXIV's native maximum debuff stack count — the ceiling for a trigger's fire count.</summary>
    public const int MaxPenumbraFireCount = 20;

    private readonly GlamourerIpc glamourerIpc;
    private readonly PenumbraIpc penumbraIpc;
    private readonly List<ActiveEffect> activeEffects = [];
    private DateTime lastCheckUtc = DateTime.UtcNow;

    public ActiveEffectRegistry(GlamourerIpc glamourerIpc, PenumbraIpc penumbraIpc)
    {
        this.glamourerIpc = glamourerIpc;
        this.penumbraIpc = penumbraIpc;
    }

    public IReadOnlyList<ActiveEffect> ActiveEffects => activeEffects;

    /// <summary>Resolves the stage entry with the greatest threshold not exceeding <paramref name="fireCount"/>,
    /// or null if the fire count hasn't reached any configured threshold yet. If multiple entries share the
    /// qualifying threshold, the last one in list order wins.</summary>
    private static PenumbraStageThreshold? ResolveStage(ReactionTrigger trigger, int fireCount)
    {
        PenumbraStageThreshold? resolved = null;
        var bestThreshold = int.MinValue;
        foreach (var stage in trigger.PenumbraStages)
        {
            if (stage.Threshold <= fireCount && stage.Threshold >= bestThreshold)
            {
                bestThreshold = stage.Threshold;
                resolved = stage;
            }
        }

        return resolved;
    }

    /// <summary>Applies whichever stage the effect's current fire count resolves to, if it differs from
    /// what's already applied. Disables the previously-active mod first when the new stage targets a
    /// different mod, so escalating across different mods never leaves an earlier one visibly active.</summary>
    private void ApplyPenumbraStage(ReactionTrigger trigger, ActiveEffect effect)
    {
        var stage = ResolveStage(trigger, effect.FireCount);
        if (stage == null)
            return;

        var unchanged = stage.ModDirectory == effect.PenumbraCurrentModDirectory
            && stage.ModName == effect.PenumbraCurrentModName
            && stage.OptionGroupName == effect.PenumbraCurrentOptionGroupName
            && stage.OptionName == effect.PenumbraCurrentOptionName;
        if (unchanged)
            return;

        var modChanged = stage.ModDirectory != effect.PenumbraCurrentModDirectory || stage.ModName != effect.PenumbraCurrentModName;
        if (modChanged && effect.PenumbraCurrentModDirectory.Length > 0)
            penumbraIpc.DisableMod(effect.PenumbraCurrentModDirectory, effect.PenumbraCurrentModName);

        penumbraIpc.SetStage(stage.ModDirectory, stage.ModName, stage.OptionGroupName, stage.OptionName);
        effect.PenumbraCurrentModDirectory = stage.ModDirectory;
        effect.PenumbraCurrentModName = stage.ModName;
        effect.PenumbraCurrentOptionGroupName = stage.OptionGroupName;
        effect.PenumbraCurrentOptionName = stage.OptionName;
    }

    public void Apply(ReactionTrigger trigger)
    {
        var existing = activeEffects.FirstOrDefault(e => e.TriggerId == trigger.Id);
        var hasStages = trigger.PenumbraStages.Count > 0;

        if (existing != null)
        {
            if (!trigger.StackMultiple)
            {
                if (trigger.RefreshOnRepeat)
                {
                    existing.ExpiresAtUtc = trigger.NoExpiration ? null : DateTime.UtcNow + trigger.Duration;
                    existing.RevertMode = trigger.GlamourerRevertMode;
                    existing.RevertToDesignId = trigger.RevertToDesignId;
                }

                if (hasStages)
                {
                    existing.FireCount = Math.Min(existing.FireCount + 1, MaxPenumbraFireCount);
                    ApplyPenumbraStage(trigger, existing);
                }

                return;
            }
        }
        else if (!trigger.StackMultiple && activeEffects.Count > 0)
        {
            // Replace-on-restack: only one non-stacking effect slot for v1.
            RevertAll();
        }

        if (trigger.GlamourerDesignId != Guid.Empty)
            glamourerIpc.ApplyDesignToLocalPlayer(trigger.GlamourerDesignId);

        var newEffect = new ActiveEffect
        {
            TriggerId = trigger.Id,
            ExpiresAtUtc = trigger.NoExpiration ? null : DateTime.UtcNow + trigger.Duration,
            RevertMode = trigger.GlamourerRevertMode,
            RevertToDesignId = trigger.RevertToDesignId,
            FireCount = 1,
        };

        if (hasStages)
            ApplyPenumbraStage(trigger, newEffect);

        activeEffects.Add(newEffect);
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
            if (effect.RevertMode == GlamourerRevertMode.SpecificDesign && effect.RevertToDesignId != Guid.Empty)
                glamourerIpc.ApplyDesignToLocalPlayer(effect.RevertToDesignId);
            else
                glamourerIpc.RevertLocalPlayer();

            if (effect.PenumbraCurrentModDirectory.Length > 0)
                penumbraIpc.DisableMod(effect.PenumbraCurrentModDirectory, effect.PenumbraCurrentModName);

            activeEffects.Remove(effect);
        }
    }

    public void RevertAll()
    {
        if (activeEffects.Count == 0)
            return;

        glamourerIpc.RevertLocalPlayer();

        foreach (var effect in activeEffects)
            if (effect.PenumbraCurrentModDirectory.Length > 0)
                penumbraIpc.DisableMod(effect.PenumbraCurrentModDirectory, effect.PenumbraCurrentModName);

        activeEffects.Clear();
    }
}
