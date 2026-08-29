## Why

A trigger's Penumbra reaction is currently always presented as an escalating stage list, even for the common case of just wanting one mod/option applied with no fire-count escalation at all. Choosing a mod/option today still means dealing with a fire-count threshold field that's irrelevant unless the trigger actually escalates. Giving the user an explicit choice between "no mod reaction," "a single mod/option," and "staged escalation" keeps the simple case simple while preserving the existing escalation feature for triggers that want it.

## What Changes

- Add a Penumbra reaction mode to each trigger: **None** (no mod reaction — today's empty-list state, unchanged), **Single** (one mod/option, applied on the trigger's first fire, no fire-count threshold shown or tracked toward escalation), or **Staged** (today's fire-count escalation across one or more tabs, unchanged).
- The Reactions section's Penumbra editor is gated by this mode: None shows just the mode choice with no further fields; Single shows flat mod/option-group/option pickers with no threshold field and no tab bar; Staged shows the existing tabbed, threshold-driven editor unchanged.
- Switching modes preserves what data reasonably carries over: Single -> Staged keeps the single entry as the first stage and reveals its (now-editable) threshold; Staged -> Single keeps only the first stage's mod/option and drops the rest; switching to None clears the configured mod/option(s).
- A trigger's existing configuration (mod/option stages already set up before this change) is interpreted as Staged mode on load, so nothing already configured changes behavior after upgrading.
- No change to Penumbra matching, application, or revert behavior for triggers that stay in Staged mode — Single mode is a presentational simplification of "one stage whose threshold is always 1," not a new runtime code path.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `trigger-config-ui`: the Penumbra reaction editor now offers a None/Single/Staged mode choice that gates which fields are shown, instead of always presenting the staged, threshold-driven editor.

## Impact

- `ReactToMe/Triggers/ReactionTrigger.cs`: add a `PenumbraReactionMode` enum (`None`, `Single`, `Staged`) field, defaulting to `None`.
- `ReactToMe/Windows/ConfigWindow.cs`: `DrawPenumbraStages` (or its caller) gains a mode selector and branches its rendering by mode; mode-switch handling trims/extends `PenumbraStages` as described above.
- No changes to `ReactToMe/Effects/ActiveEffectRegistry.cs` or `ReactToMe/Ipc/PenumbraIpc.cs` — stage resolution and application logic are unaffected; Single mode reuses the existing single-stage-at-threshold-1 code path.
