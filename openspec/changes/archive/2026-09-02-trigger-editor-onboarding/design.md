## Context

Both capabilities land entirely inside `ReactToMe/Windows/ConfigWindow.cs`. Two existing conventions in that file are the load-bearing precedent for this design:

- **Discoverable hint text** already exists in one place: the `Penumbra reaction` section label is drawn with `ImGui.TextDisabled(...)` followed by `IsItemHovered()`/`SetTooltip(...)` (`ConfigWindow.cs:1187-1189`). Every other tooltip in the file is attached directly to the field's own input widget, with no visible marker — which is exactly what makes it undiscoverable (see proposal.md - Why).
- **Modal popups** already exist in one place: `DrawOverlayPreviewPopup` (`ConfigWindow.cs:364-389`), opened via a `bool ...ShouldOpen` flag checked once per frame → `ImGui.OpenPopup(id)`, then `ImGui.BeginPopupModal(id, ref open)` / `EndPopup()` each frame, with `open` wired to the popup's own close button.

## Goals / Non-Goals

**Goals:**
- Reuse both existing conventions rather than inventing new ones, so the change reads as "more of the same idiom," not a new UI pattern to maintain.
- Keep the tutorial's content fully self-contained (no reads of `configuration.Triggers` or `selectedTriggerId`), so "read-only" is true by construction rather than by discipline.

**Non-Goals:**
- No first-run auto-popup or "seen it" persistence in `Configuration` — the tutorial is opened only by clicking the button, every time (see spec: "Walkthrough is reachable at any time, not just once").
- No live binding between the tutorial's example content and a real trigger's current field values — it teaches the concept with illustrative example values, not a live-data walkthrough.
- No change to `ReactionTrigger`, `Configuration`, `TriggerMatcher`, or `ActiveEffectRegistry`.

## Decisions

### A reusable `DrawHelpMarker(string tooltip)` helper, generalized from the existing `Penumbra reaction` idiom
Add one private helper that draws a small muted `(?)` via `ImGui.SameLine()` + `ImGui.TextDisabled("(?)")` + `IsItemHovered()`/`SetTooltip(tooltip)`, called right after a field whose tooltip should become discoverable. This replaces scattered `if (ImGui.IsItemHovered()) ImGui.SetTooltip(...)` calls that currently follow the field's own widget, for every field the modified `trigger-config-ui` spec requirement now covers: the fields that already had a tooltip, plus the two that had none (`Refresh timer on repeat`, `Stack multiple`).

**Alternative considered:** wrap every field in a wider "info panel" row instead of an inline marker. Rejected — the pane is already dense (see proposal.md - Why); an inline glyph adds one small element per field instead of restructuring layout, keeping this change purely additive as scoped.

### Duration's guidance moves to the `"Duration"` label itself, not its h/m/s sub-fields
Today's `IsItemHovered()` call sits after all three `InputInt` calls, so it only ever fires for the last-drawn widget (the seconds box) — `ConfigWindow.cs:1102-1119`. Attach `DrawHelpMarker` immediately after the `ImGui.TextUnformatted("Duration")` call instead, so the affordance (and its tooltip) is anchored to the label, independent of which sub-field the user is looking at.

### Tutorial modal reuses the `DrawOverlayPreviewPopup` open/draw pattern
Add a `bool tutorialPopupShouldOpen` field set by the new `Tutorial` button in `DrawTriggersTab`, checked once per frame to call `ImGui.OpenPopup("Trigger Tutorial")`, and a `DrawTutorialPopup()` method following the exact `BeginPopupModal(id, ref open)` / `EndPopup()` shape already used for the overlay preview. This means no new window-lifecycle code, and the modal automatically inherits `PurpleTheme` like every other popup in the file (pushed once around `WindowSystem.Draw()` in `Plugin.DrawWindows`).

**Alternative considered:** a dedicated `TutorialWindow : Window` registered in `WindowSystem`, mirroring `ConfigWindow` itself. Rejected — that's real machinery (open/close state tracked by `WindowSystem`, its own docking/sizing behavior) for content that's fundamentally a short, linear, dismissible sequence; a modal is the lighter-weight fit and matches the one precedent already in the file.

### Step content is a small static array, not derived from live trigger data
The walkthrough's three steps (Source, Reaction, Timing) are hardcoded example content — sample field names and sample values, not `configuration.Triggers` or Lumina-sheet lookups. A `tutorialStepIndex` field (reset to `0` every time the popup opens) drives which step's static content renders, with `Back`/`Next` buttons clamping it to `[0, stepCount)` and a `Done` button on the final step calling `ImGui.CloseCurrentPopup()`.

**Alternative considered:** walk the user through by highlighting/scrolling to the real fields in the detail pane as they step through. Rejected as out of scope — the proposal deliberately chose "pure walkthrough, no trigger created," and coupling the wizard to `selectedTriggerId`/live fields would both violate that and reintroduce the exact "must have a trigger selected to learn how triggers work" chicken-and-egg problem a newbie already hits today.

## Risks / Trade-offs

- **Static tutorial content can drift from the real field set** as the trigger model grows (new source types, new reaction kinds) → since it's illustrative/conceptual rather than enumerating exact current labels, it degrades gracefully rather than becoming flatly wrong; still worth a quick glance whenever `ReactionTrigger`'s source/reaction fields change.
- **Adding a `(?)` marker to many fields risks visual noise** in a pane the proposal is trying to make feel less dense → mitigated by only applying it where the modified spec requires it (fields that aren't self-explanatory from their label alone), not universally, and by using the same muted `TextDisabled` styling already established for `Penumbra reaction` so it reads as a consistent, low-weight affordance rather than new decoration.
- **`ImGui.SameLine()` placement after existing fields could disturb alignment** in rows that already chain multiple `SameLine` calls (e.g. the Duration h/m/s row, the `Refresh timer on repeat` / `Stack multiple` row) → no automated layout test exists for this ImGui UI; verify visually in-game per affected row while implementing, same as the rest of this file's UI work.

## Migration Plan

None needed — no persisted config shape changes, no version bump. Ships as a UI-only patch to `ConfigWindow.cs`; rollback is reverting that file.
