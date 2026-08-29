## 1. Penumbra IPC integration

- [x] 1.1 Add a `Penumbra.Api` package reference to `ReactToMe/ReactToMe.csproj` (matching the version pattern already used for `Glamourer.Api`) and verify the project restores/builds with the new dependency
- [x] 1.2 Create `ReactToMe/Ipc/PenumbraIpc.cs` mirroring `GlamourerIpc`/`MoodlesIpc`'s shape: wrap `GetCollectionForObject` (local player), `GetModList`, `GetAvailableModSettings`, `TrySetMod`, and `TrySetModSetting`, each with the existing try/catch + log + chat-warning pattern on IPC failure, and verify by calling `GetModList()` in a manual test and confirming it returns real installed mods
- [x] 1.3 Add a method that enables a mod and sets its option group to a given option name in one call (resolving the local player's collection internally), and verify by manually driving one mod's option group to a specific value through it

## 2. Trigger model

- [x] 2.1 Add a `PenumbraStageThreshold` type to `ReactToMe/Triggers/ReactionTrigger.cs` with `Threshold` (int, 1-20), `ModDirectory`, `ModName`, `OptionGroupName`, and `OptionName` — each stage is fully self-contained so entries can target the same mod/group, different groups of the same mod, or entirely different mods
- [x] 2.2 Add a `PenumbraStages` field (`List<PenumbraStageThreshold>`) to `ReactionTrigger`, defaulting to empty, and update `HasAnyAction` to also be true when it's non-empty; verify existing trigger JSON config still deserializes without the field present (old configs)

## 3. Fire count and threshold resolution

- [x] 3.1 On `ActiveEffect` (`ReactToMe/Effects/ActiveEffectRegistry.cs`), add `FireCount` (int, starts at 1) and `PenumbraCurrentModDirectory`/`PenumbraCurrentModName`/`PenumbraCurrentOptionGroupName`/`PenumbraCurrentOptionName` (track the full stage last actually applied, empty until the first threshold is met); verify a newly-applied staged trigger's effect starts at fire count 1
- [x] 3.2 Add a helper that resolves the active `PenumbraStageThreshold` entry for a given fire count: the entry with the greatest `Threshold <= fireCount` (or `null` if none qualify yet; if multiple entries share the qualifying threshold, the last one in list order wins); verify with a few fire-count values against a sample threshold list (e.g. 1/5/10)
- [x] 3.3 In `Apply()`'s repeat-fire branch (`existing != null && !trigger.StackMultiple`), increase `existing.FireCount` by one when the trigger has any stages configured, capped at 20 (independent of `RefreshOnRepeat`); verify by firing the same trigger repeatedly and observing the fire count increase then hold at 20
- [x] 3.4 After updating the fire count (on first apply and on every repeat fire), resolve the stage via 3.2 and, only if its mod/group/option differs from what's currently applied: disable the previously-applied mod first if the mod itself changed, then call the Penumbra IPC method from 1.3 with the new stage's mod/group/option and update the tracked "currently applied" fields; skip entirely when no stage qualifies yet; verify escalating within one mod only updates its option group (mod stays enabled throughout) while escalating to a different mod disables the old one first
- [x] 3.5 In `Tick()`'s expiry handling and in `RevertAll()`, disable whichever mod is currently active for the effect (via the tracked `PenumbraCurrentModDirectory`/`PenumbraCurrentModName`) alongside the existing Glamourer revert; verify by letting a staged trigger's timer expire (or manually force-reverting) and confirming the active mod is disabled in Penumbra

## 4. Config UI

- [x] 4.1 Replace the single shared mod/group picker with a per-trigger list of stage rows in `ConfigWindow.Draw()` (`ReactToMe/Windows/ConfigWindow.cs`) bound to `trigger.PenumbraStages`; each row has its own fire-count threshold input, a searchable mod picker (reusing `DrawSearchablePicker` against `PenumbraIpc.GetModList()`), an option-group picker scoped to that row's mod, and an option picker scoped to that row's group; verify changes persist via `configuration.Save()` like the other controls in that loop
- [x] 4.2 Add/remove controls for stage rows (no reorder needed, since threshold value — not list position — determines resolution order); "Add stage" defaults a new row's mod/group to the previous row's, so the common "one mod, several options" case only requires re-picking the option and threshold
- [x] 4.3 Add a tooltip explaining that repeat fires increase a fire count (capped at 20), the highest-threshold stage reached is applied, switching to a stage with a different mod disables the previous one, and the trigger's existing timer (or manual force-revert) disables whichever mod is currently active

## 5. End-to-end verification

- [x] 5.1 Manually verify the scenario groups in `specs/penumbra-mod-staging/spec.md`: no-op without any staged entries, fire count below the lowest threshold applies nothing, reaching a threshold applies its stage, fire count between thresholds holds the last-reached stage, escalating within the same mod doesn't disable it, escalating to a different mod disables the previous one, the fire count caps at 20, timer expiry disables the active mod, a no-expiration trigger keeps its stage indefinitely, and two different staged triggers track their fire counts independently
