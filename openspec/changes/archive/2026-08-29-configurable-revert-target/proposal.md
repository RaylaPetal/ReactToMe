## Why

Today, whenever a trigger's applied Glamourer design expires (or is force-reverted), ReactToMe always calls Glamourer's "revert to automation," handing control back to whatever Glamourer's own automation system defines. There's no way to instead land on a specific, deliberately-chosen "base" look — useful when a user's idle appearance isn't managed by Glamourer automation at all, or when they want a trigger's reaction to end on a specific baseline design rather than whatever automation currently resolves to.

## What Changes

- Add a per-trigger revert-target choice: "Revert to automation" (default — current, unchanged behavior) or "Apply a specific design" (pick a Glamourer design to apply on expiry instead of reverting to automation).
- When a trigger's timer expires, its applied effect reverts using that trigger's configured choice at the time it was applied — either Glamourer's revert-to-automation call, or an apply-design call targeting the configured base design.
- The existing manual "Force revert all" button continues to always perform a hard revert-to-automation, regardless of any trigger's custom base-design setting, since it exists specifically as a safety escape hatch (per the plugin's original design principle that timers must never trap someone in a state they can't back out of).
- No changes to Moodles integration or the no-expiration option: a trigger with no expiration never reverts, so its revert-target choice is irrelevant until expiration is possible.
- `ConfigWindow` gets a revert-target mode selector per trigger, with a design picker shown when "Apply a specific design" is chosen, reusing the existing searchable design-picker UI already used for a trigger's own applied design.

## Capabilities

### New Capabilities
- `revert-target`: per-trigger choice of what Glamourer state a trigger's expiry reverts to — automation (default) or a specific base design.

### Modified Capabilities
- (none — this only adds a new choice to expiry behavior; it does not change the `moodle-duration-lock` capability's requirements)

## Impact

- `ReactToMe/Triggers/ReactionTrigger.cs` — new revert-mode field (automation vs. specific design) and a `RevertToDesignId` field, defaulting to automation so existing configs are unaffected.
- `ReactToMe/Effects/ActiveEffectRegistry.cs` — each `ActiveEffect` captures the owning trigger's revert-target choice at apply time; `Tick()`'s expiry path uses it to decide which Glamourer IPC call to make. `RevertAll()` (the manual escape hatch) is unaffected and keeps calling revert-to-automation unconditionally.
- `ReactToMe/Ipc/GlamourerIpc.cs` — no new IPC calls; the existing `ApplyDesignToLocalPlayer` is reused for the "apply a specific design" revert path.
- `ReactToMe/Windows/ConfigWindow.cs` — new revert-target selector and conditional design picker per trigger.
