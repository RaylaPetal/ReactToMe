## Context

See proposal.md - Why. `ActiveEffectRegistry.Tick()` currently reverts every expired effect the same way: an unconditional `glamourerIpc.RevertLocalPlayer()` call (Glamourer's revert-to-automation IPC). `ActiveEffect` already bakes trigger-configured values in at `Apply()` time rather than re-reading the trigger later — `ExpiresAtUtc` is computed from `trigger.Duration`/`trigger.NoExpiration` once, at apply time, and `Tick()` never looks the trigger back up. `GlamourerIpc` already exposes `ApplyDesignToLocalPlayer(Guid)`, used today to apply a trigger's own design — no new Glamourer IPC surface is needed for this change.

## Goals / Non-Goals

**Goals:**
- Let each trigger's expiry resolve to either automation or a specific chosen design, decided per-trigger.
- Keep the manual "force revert all" escape hatch unconditionally reverting to automation.
- Follow the existing bake-at-apply-time pattern rather than introducing a trigger lookup at tick time.

**Non-Goals:**
- Changing how the trigger's own applied design is chosen or applied — unaffected.
- Any interaction with Moodles or the no-expiration option beyond what already exists (a no-expiration effect never reverts, so its revert target is simply never used).

## Decisions

1. **Capture the revert target on `ActiveEffect` at `Apply()` time**, alongside `ExpiresAtUtc`, rather than looking the owning trigger back up from `Tick()`. This matches the existing pattern (`Duration`/`NoExpiration` are already baked in the same way) and avoids giving `ActiveEffectRegistry` a dependency on trigger storage it doesn't otherwise need. Trade-off: if a trigger's revert-target setting is edited while one of its effects is already active, that in-flight effect keeps the value captured when it was applied — consistent with how editing `Duration` mid-timer today doesn't retroactively change an already-running effect's expiry.
2. **Represent the choice as a mode (`Automation` / `SpecificDesign`) plus a `Guid` design id**, rather than overloading `Guid.Empty` on a single field, so the UI and the tick logic have an explicit tri-state-free choice to branch on.
3. **Fall back to automation if `SpecificDesign` is selected but no design is actually chosen** (`RevertToDesignId == Guid.Empty`) — safer than calling Glamourer's apply-design IPC with an empty id, and keeps the trigger from silently doing nothing on expiry.
4. **`RevertAll()` stays exactly as-is** (an unconditional `RevertLocalPlayer()` call, ignoring per-effect state) — this already satisfies "always hard-reset to automation" with no code change, since it never reads the per-effect revert target today and won't need to.

## Risks / Trade-offs

- [Risk] The chosen base design could be deleted or renamed in Glamourer between configuration and expiry → Mitigation: `ApplyDesignToLocalPlayer` already has a try/catch that logs and shows the existing "Could not apply Glamourer design" chat warning; no new failure path needed.
- [Risk] A trigger left in `SpecificDesign` mode with no design picked would otherwise silently do nothing on expiry → Mitigation: falls back to automation (Decision 3).
