## Why

The Overlay Mod Builder tab has the same onboarding problem the trigger editor had before `trigger-editor-onboarding`: most of its buttons and fields already carry an explanatory tooltip, but that guidance is only reachable by incidentally hovering the right widget, with no visible cue that it exists. A few fields (`Display name`, a stage's `Name`, `Add Project`, `Add Stage`) have no guidance at all. And unlike triggers, this tab has never had any onboarding treatment — there is no discoverable-guidance requirement in its spec and no guided walkthrough for a newbie building their first overlay project through its multi-step, order-dependent workflow (pick a target texture, capture a snapshot, add stages, apply).

## What Changes

- Add the same discoverable help-marker affordance (the `(?)` glyph introduced for triggers) next to Overlay Mod Builder fields whose purpose isn't obvious from their label alone, converting every existing hover-only tooltip in `DrawOverlayProjectDetail`/`DrawOverlayModBuilderTab` to use it.
- Add missing tooltip guidance for the fields that currently have none: `Display name`, a stage's `Name` field, `Add Project`, `Add Stage`.
- Add a `Tutorial` button to the Overlay Mod Builder tab header (opposite `Add Project`), opening a self-contained, read-only guided walkthrough covering the common project-building path: picking a base texture, capturing a snapshot, adding a stage with an overlay image, and applying. The walkthrough creates no project and touches no project state. `Priority`, `Penumbra folder`, and `Recreate Mod`/`Rebake All` (recovery-oriented, not part of the first-build path) are out of its scope — those stay explained via the help-marker affordance above, mirroring how the trigger tutorial excluded Penumbra staging/revert-to-design/stacking.

## Capabilities

### New Capabilities
- `overlay-mod-builder-tutorial`: a guided, read-only walkthrough reachable from the Overlay Mod Builder tab that explains the common project-building path (target texture, snapshot, one stage, apply) without creating a project.

### Modified Capabilities
- `overlay-mod-builder`: adds a discoverable hover-guidance requirement — this capability has never had one — requiring a visible help-marker affordance on non-obvious fields, mirroring the requirement already established for `trigger-config-ui`.

## Impact

- `ReactToMe/Windows/ConfigWindow.cs`: `DrawOverlayModBuilderTab` and `DrawOverlayProjectDetail` gain new/relocated `DrawHelpMarker` calls (reusing the helper added for `trigger-editor-onboarding`) and a new tutorial button + modal (reusing the `DrawTutorialPopup` open/draw pattern already established for the trigger tutorial). Purely additive UI — no changes to `OverlayModBuilderProject`, `OverlayModBuilderStage`, `OverlayModBuilderService`, or any baking/apply logic.
