## Why

The trigger detail pane packs source, reaction, and timing/revert fields into one dense layout, and while most fields already carry an explanatory tooltip, that guidance is only reachable by incidentally hovering the right widget — there's no visible cue telling a newbie which labels have more behind them. A few fields (`Refresh timer on repeat`, `Stack multiple`) have no guidance at all, and the `Duration` control's tooltip only fires from its trailing seconds box, not the `Duration` label itself. There's also no guided entry point for a first-time user to understand the trigger model (source -> reaction -> timing) before building one from a blank form.

## What Changes

- Add a discoverable help-marker affordance (a small `(?)` glyph, matching the existing `Penumbra reaction` label's grayed-hint convention) next to detail-pane fields whose purpose isn't obvious from their label alone, so guidance is visible without relying on incidental hovering.
- Add missing tooltip guidance for the `Refresh timer on repeat` and `Stack multiple` checkboxes.
- Fix the `Duration` control so its guidance is reachable by hovering the `Duration` label itself, not only its trailing seconds sub-field.
- Add a `Tutorial` button to the Triggers tab header (opposite `Add Trigger`) that opens a self-contained, read-only guided walkthrough covering the common trigger-building path: choosing a source, picking one reaction, and setting a duration. The walkthrough does not create or modify any trigger, and Penumbra staging / revert-to-design / stacking are out of its scope — those stay explained via the help-marker affordance above.

## Capabilities

### New Capabilities
- `trigger-editor-tutorial`: a guided, read-only walkthrough reachable from the Triggers tab that explains the common trigger-building path (source, one reaction, duration) without creating a trigger.

### Modified Capabilities
- `trigger-config-ui`: the existing "Fields offer hover guidance" requirement is sharpened so guidance is discoverable via a visible affordance rather than only reachable by incidental hovering, and extended to cover fields that currently have none (`Refresh timer on repeat`, `Stack multiple`) and one whose guidance is currently mis-anchored (`Duration`).

## Impact

- `ReactToMe/Windows/ConfigWindow.cs`: `DrawTriggerDetail`, `DrawReactionsSection`, `DrawTimingSection`, and `DrawTriggersTab` gain a reusable help-marker helper, new/relocated tooltip calls, and a new tutorial button + modal. Purely additive UI — no changes to `ReactionTrigger`, `Configuration`, or any matching/effect logic.
