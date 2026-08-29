## Why

A trigger's Penumbra stages are drawn as one long vertical block per stage (fire count, mod picker, option group, option, remove button, separator) inside the Reactions section of the detail pane. With several stages configured — the whole point of the fire-count escalation feature — this list grows long and pushes the rest of the detail pane's fields far down, making the trigger hard to scan or edit.

## What Changes

- Rework the Penumbra stages editor to present each stage as its own tab (mirroring the existing top-level Active Effects / Triggers / Settings tab bar), instead of a stacked list of full stage blocks.
- Each stage tab's label reflects that stage's fire-count threshold, so stages stay identifiable at a glance without opening them.
- Adding and removing a stage adds/removes its tab; removing the currently open stage's tab selects an adjacent tab (or shows an empty/add-prompt state if no stages remain).
- No change to the underlying stage data model, matching behavior, or IPC calls — this is purely a presentation change to `specs/trigger-config-ui`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `trigger-config-ui`: the Penumbra stages editor within the Reactions section is now a tab bar of per-stage tabs, rather than a stacked list of full stage blocks.

## Impact

- `ReactToMe/Windows/ConfigWindow.cs`: `DrawPenumbraStages` reworked to render a nested `ImGui.BeginTabBar`/`TabItem` per stage instead of a `for` loop of stacked blocks; add-stage and remove-stage controls adapt to the tab layout.
- No changes to `ReactToMe/Triggers/ReactionTrigger.cs`, `ActiveEffectRegistry`, or `PenumbraIpc` — stage data and application logic are unaffected.
