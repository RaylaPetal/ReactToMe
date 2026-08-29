## Context

See proposal.md - Why. `ActiveEffectRegistry` currently tracks `ExpiresAtUtc`/`RevertMode`/`RevertToDesignId` per `ActiveEffect`, computed at `Apply()` time; a repeat fire of a non-stacking trigger short-circuits early (the `existing != null` branch) and optionally refreshes the timer/revert-target if `RefreshOnRepeat` is set, but otherwise does nothing else today.

Penumbra publishes a `Penumbra.Api` NuGet package (like `Glamourer.Api`, unlike Moodles) exposing plain-typed IPC calls relevant here, confirmed against Penumbra's public source (xivdev/Penumbra, `Api/Api/{CollectionApi,ModsApi,ModSettingsApi}.cs`):
- `GetCollectionForObject(gameObjectIdx) -> (bool ObjectValid, bool IndividualSet, (Guid Id, string Name) EffectiveCollection)` — resolves the active collection for a game object (the local player, using the same object-index pattern already used for Glamourer).
- `GetModList() -> Dictionary<string, string>` (modDirectory -> modName) — lists installed mods.
- `GetAvailableModSettings(modDirectory, modName) -> AvailableModSettings?` (group name -> (option names, group type)) — lists a mod's option groups and their options.
- `TrySetMod(collectionId, modDirectory, modName, enabled) -> PenumbraApiEc` — enables/disables a mod.
- `TrySetModSetting(collectionId, modDirectory, modName, optionGroupName, optionName) -> PenumbraApiEc` — sets a single-select option group's selected option.

All of these use only Guid/string/bool/Dictionary types, so — unlike the Moodles duration investigation — there's no private-enum cross-plugin type mismatch blocking their use here.

## Goals / Non-Goals

**Goals:**
- Drive Penumbra mods through configured stages as a trigger repeatedly fires, using only Penumbra's published, plain-typed IPC surface — covering escalation within one mod's option group, across different option groups of one mod, and across entirely different mods, with one mechanism.
- Reuse the existing revert-timer lifecycle (Duration/NoExpiration/manual force-revert) for resetting the currently active stage, rather than inventing a second timer.

**Non-Goals:**
- Reading a Moodle's own stack count from Moodles — out of scope; ReactToMe tracks its own fire count instead. Revisit only if Moodles later publishes a types-safe API.
- Full escalation semantics for `StackMultiple` triggers beyond "each new stacked instance starts at fire count 1."
- Any Penumbra collection other than the local player's own effective collection.

## Decisions

1. **Track a `FireCount` (int, 1-20) on `ActiveEffect`**, incremented in the same `existing != null && !trigger.StackMultiple` branch in `Apply()` that already short-circuits repeat fires — independent of `RefreshOnRepeat`, since fire-count progression represents "the action happened again," not "the visual timer should restart." A repeat fire always increases the fire count (capped at 20, FFXIV's native maximum debuff stack count), even when `RefreshOnRepeat` is off and the Duration timer is left alone. The active stage for a given fire count is resolved by finding the configured entry with the greatest `Threshold` that is `<= FireCount`.
2. **Each stage entry is fully self-contained** — `(Threshold, ModDirectory, ModName, OptionGroupName, OptionName)` — rather than the trigger naming one shared mod/group that all stages funnel through. This is what lets the same mechanism cover escalating within one mod's option group, escalating across different option groups of one mod, and escalating across entirely different mods: all three are just "the resolved entry's mod/group/option changed (or didn't)," with no separate code path per case. `ActiveEffect` tracks the full `(ModDirectory, ModName, OptionGroupName, OptionName)` last actually applied, so Penumbra's set-option IPC is only called when the resolved entry differs from that, not on every fire.
3. **Disable the previous stage's mod only when the mod itself changes.** On a resolved-stage change, compare the new entry's `(ModDirectory, ModName)` against what's currently applied: if they match, just update the option group (the mod stays enabled throughout — covers escalating within one mod, whether the same group or a different one); if they differ, disable the old mod first, then enable and set the new one (covers escalating across different mods, so an earlier mod is never left visibly active once superseded).
4. **Resolve the Penumbra collection via `GetCollectionForObject(LocalPlayerObjectIndex)` at the point of use**, reusing the `LocalPlayerObjectIndex = 0` constant pattern already in `GlamourerIpc`, rather than asking the user to paste a collection GUID — matches the existing "local player" auto-resolution UX already used for Glamourer and Moodles.
5. **"Reset" means disabling the currently active stage's mod (`TrySetMod(..., enabled: false)`), not selecting a "stage 0" option.** Penumbra's option-group model has no built-in "none" state for a single-select group unless the mod author added one; disabling the mod is the universal, always-available "off" state — it works regardless of how the mod's option group is authored, and avoids requiring the user to define an extra placeholder option.
6. **`PenumbraIpc` mirrors the existing `MoodlesIpc`/`GlamourerIpc` shape**: thin wrapper methods over `Penumbra.Api`'s IPC subscribers, each with a try/catch that logs and shows the same chat-warning style on failure (Penumbra not installed/loaded), consistent with the rest of the codebase.
7. **Stage configuration is a `List<PenumbraStageThreshold>` of fully self-contained entries on the trigger** (no trigger-level shared mod/group), each populated from the mod's actual option list via `GetAvailableModSettings` in the config UI, but not re-validated against the mod at fire time — a stale/removed mod, group, or option simply fails the same way any other missing Penumbra reference would (existing IPC-failure chat-warning path), rather than needing new validation logic. Entries need not be stored in threshold order; resolution always scans for the greatest qualifying threshold, so the config UI doesn't need a strict-order or reorder affordance — only add/remove.
8. **The 20-cap is fixed, not user-configurable**, matching FFXIV's actual native maximum debuff stack count exactly, so there's one fewer per-trigger setting and no way to configure a cap that doesn't correspond to anything the game itself supports.

## Risks / Trade-offs

- [Risk] The user reconfigures or removes a stage's mod/group/option after it was selected, leaving a stored reference that no longer exists → Mitigation: `TrySetModSetting`/`TrySetMod` return a non-success `PenumbraApiEc` (e.g. a mod- or option-missing code) in that case, surfaced through the existing IPC-failure chat-warning pattern rather than a crash.
- [Risk] A trigger's staged mods and its Glamourer/Moodle actions are now both driven by the same `Apply()`/expiry lifecycle on one `ActiveEffect`, even though they're conceptually separate concerns → Mitigation: acceptable for v1, consistent with how `RevertMode`/`RevertToDesignId` were already added to the same class; a larger per-action-type refactor is out of scope here.
- [Risk] Configuring the config UI's per-row mod/group/option pickers (three cascading dropdowns per stage) is more clicks than a single shared mod/group picker would have been for the common "one mod, several options" case → Mitigation: "Add stage" defaults a new row's mod/group to the previous row's, so the common case only requires re-picking the option.
- [Risk] Combining staging with `StackMultiple` has only minimal, explicitly partial semantics (each stacked instance starts at fire count 1) → Mitigation: called out as a Non-Goal; an unusual combination the original request didn't ask for.
- [Risk] Two configured stage entries could share the same threshold, making resolution ambiguous → Mitigation: resolution deterministically prefers the last matching entry in list order; not surfaced as a validation error, since it's a harmless (if confusing) misconfiguration rather than a broken one.
