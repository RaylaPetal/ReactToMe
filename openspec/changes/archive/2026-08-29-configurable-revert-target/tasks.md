## 1. Trigger model

- [x] 1.1 Add a `GlamourerRevertMode` enum (`Automation`, `SpecificDesign`) and use it as a new field on `ReactionTrigger` (`ReactToMe/Triggers/ReactionTrigger.cs`), defaulting to `Automation`, and verify existing trigger JSON config still deserializes without the new field present (old configs)
- [x] 1.2 Add a `RevertToDesignId` `Guid` field to `ReactionTrigger`, defaulting to `Guid.Empty`, relevant only when `GlamourerRevertMode` is `SpecificDesign`

## 2. Revert-timer logic

- [x] 2.1 Add `RevertMode` and `RevertToDesignId` fields to `ActiveEffect` (`ReactToMe/Effects/ActiveEffectRegistry.cs`), captured from the trigger in `Apply()` at the same point `ExpiresAtUtc` is computed, and verify by inspecting a newly-applied effect's captured values match the triggering trigger's current settings
- [x] 2.2 In `Tick()`'s expiry handling, call `glamourerIpc.ApplyDesignToLocalPlayer(effect.RevertToDesignId)` when the expiring effect's `RevertMode` is `SpecificDesign` and `RevertToDesignId != Guid.Empty`, and `glamourerIpc.RevertLocalPlayer()` otherwise (covers `Automation` mode and the empty-design fallback); verify both paths with a manual test (one trigger set to automation, one set to a specific design)
- [x] 2.3 Confirm `RevertAll()` is left unchanged (no per-effect revert-target branching), so the manual "Force revert all" button keeps unconditionally reverting to automation; verify by manually force-reverting while an active effect's trigger is set to a specific-design revert target and observing automation is applied, not that design

## 3. Config UI

- [x] 3.1 Add a revert-target mode selector ("Revert to automation" / "Apply a specific design") per trigger in `ConfigWindow.Draw()` (`ReactToMe/Windows/ConfigWindow.cs`), near the existing Duration/No-expiration controls, bound to `trigger.GlamourerRevertMode`, and verify it persists via `configuration.Save()` like the other controls in that loop
- [x] 3.2 When the mode is `SpecificDesign`, show a design picker (reusing the existing `DrawSearchablePicker` pattern already used for the trigger's own applied design) bound to `trigger.RevertToDesignId`, hidden when the mode is `Automation`
- [x] 3.3 Add a tooltip on the mode selector explaining that "Force revert all" always goes to automation regardless of this setting

## 4. End-to-end verification

- [x] 4.1 Manually verify the three scenario groups in `specs/revert-target/spec.md`: default behavior is unchanged (reverts to automation), a trigger set to a specific design applies that design on timer expiry, and manual force-revert always goes to automation even when an active trigger is set to a specific design
