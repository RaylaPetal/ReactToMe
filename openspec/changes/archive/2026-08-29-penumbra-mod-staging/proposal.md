## Why

Some effects aren't a single on/off look — a mod like a body-marks overlay exposes a single option group with several intensity levels (e.g., "1"/"2"/"3"), and repeating the same action (another spank, another whip) should visibly escalate through those levels rather than just re-applying the same single state. ReactToMe has no way to drive a Penumbra mod's option group at all today, and no concept of a trigger's effect escalating across repeated fires — every action is currently apply-once/revert-once.

## What Changes

- Add a new independent trigger action — "staged Penumbra mod" — alongside the existing Glamourer design, Moodle, and chat message actions: a set of stage entries, each pairing a fire-count threshold with its own fully self-contained Penumbra target (mod, option group, and option). Stages are independent of each other, so the same trigger can escalate within one mod's option group, across different option groups of one mod, or across entirely different mods — whichever fits the mod setup being driven.
- Each repeat fire of a trigger (while it's already active and not stacking) increases that trigger's own fire count by one, capped at 20 — matching FFXIV's native maximum debuff stack count. The fire count does not wrap or reset on its own.
- The active stage at any moment is whichever configured entry has the greatest threshold not exceeding the current fire count (e.g., thresholds 1/5/10 mean fire counts 1-4 apply the threshold-1 stage, 5-9 apply the threshold-5 stage, 10+ apply the threshold-10 stage). If the fire count hasn't reached any configured threshold yet, no stage is applied. Users choose their own threshold numbers — 1/5/10 is an example, not a fixed scheme.
- Whenever the resolved stage differs from what's currently applied, the system enables that stage's configured mod (if not already enabled) and sets its option group to that stage's option, via Penumbra's IPC. If the new stage's mod differs from the previously active stage's mod, the previous mod is disabled first, so escalating across different mods never leaves an earlier one visibly active. Fires that don't cross into a new stage don't re-issue any IPC call.
- When the trigger's revert timer expires (the same Duration/no-expiration timer already governing the Glamourer revert), the currently active stage's mod is also reset: disabled via Penumbra's IPC, alongside the existing Glamourer revert. A trigger with no expiration keeps its current stage indefinitely, same as it keeps its Glamourer effect.
- Stage progress is scoped per-trigger — each configured trigger (e.g. one per emote) tracks its own independent fire count and its own list of stage entries; different triggers never share a counter.
- Combining staging with a trigger's existing "stack multiple" option is out of scope for this change: each independently-stacked instance of such a trigger simply starts at fire count 1, since stage progress is only meaningful for a single ongoing effect, not for independently-stacked ones.
- `ConfigWindow` gets a new section per trigger: an editable list of stage entries, each with its own fire-count threshold, mod picker, option-group picker (scoped to that row's mod), and option picker (scoped to that row's group).

## Capabilities

### New Capabilities
- `penumbra-mod-staging`: per-trigger Penumbra stages — each its own mod/group/option — that escalate through user-defined fire-count thresholds as the trigger repeatedly fires (capped at 20, FFXIV's native stack maximum), and reset when the trigger's timer expires.

### Modified Capabilities
- (none — this adds a new, independent action type; it does not change the `revert-target` or `moodle-duration-lock` capabilities' requirements)

## Impact

- `ReactToMe/Triggers/ReactionTrigger.cs` — new field: a list of stage entries, each with its own threshold, mod directory/name, option group name, and option name. Optional, like the existing action fields.
- `ReactToMe/Ipc/PenumbraIpc.cs` (new) — wraps Penumbra's published `Penumbra.Api` package: resolving the local player's active collection, listing installed mods (for the picker), listing a mod's option groups/options (for the picker), enabling/disabling a mod, and setting an option group's selected option.
- `ReactToMe/Effects/ActiveEffectRegistry.cs` — tracks each active effect's fire count (capped at 20) and the currently-applied stage's full mod/group/option; `Apply()` increases the fire count on repeat fires and calls Penumbra's set-option IPC only when the resolved stage changes, disabling the previous mod first if the mod itself changed; `Tick()`'s expiry path (and `RevertAll()`) disables whichever mod is currently active alongside the existing Glamourer revert.
- `ReactToMe/ReactToMe.csproj` — new `Penumbra.Api` package reference.
- `ReactToMe/Windows/ConfigWindow.cs` — new per-trigger stage-list UI, each row with its own threshold, mod picker, option-group picker, and option picker.
