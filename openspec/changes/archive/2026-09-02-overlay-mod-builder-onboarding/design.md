## Context

Both capabilities land in `ReactToMe/Windows/ConfigWindow.cs`, alongside the machinery `trigger-editor-onboarding` already added there for the same purpose on the Triggers tab:

- `DrawHelpMarker(string tooltip)` — the discoverable `(?)` affordance (`ImGui.SameLine()` + `ImGui.TextDisabled("(?)")` + `IsItemHovered()`/`SetTooltip(...)`). Already exists and needs no changes; this change only wires it into `DrawOverlayModBuilderTab`/`DrawOverlayProjectDetail` fields.
- `TutorialStep` (a `Title`/`Body` record struct) and `DrawTutorialPopup()` — the trigger tutorial's static-content, `Back`/`Next`/`Done` modal, built on the pre-existing `DrawOverlayPreviewPopup` open/draw pattern (`bool ...ShouldOpen` flag → `ImGui.OpenPopup` → `BeginPopupModal`/`EndPopup`).

`DrawTutorialPopup()` today is hardcoded to the trigger tutorial's own popup id, step array, and step-index field. This change needs a second, independent tutorial (different content, different popup, opened from a different tab), which is what most of the "Decisions" section below is about.

## Goals / Non-Goals

**Goals:**
- Reuse `DrawHelpMarker` as-is — no changes to it.
- Generalize the one-off `DrawTutorialPopup()` into something both the existing trigger tutorial and this new overlay tutorial can use, rather than copy-pasting a second ~40-line stepper.
- Keep the overlay tutorial's content fully static (no reads of `configuration.OverlayModBuilderProjects` or `selectedOverlayProjectId`, no Penumbra/IPC calls), so "read-only" holds by construction, same guarantee `trigger-editor-tutorial` made.

**Non-Goals:**
- No first-run auto-popup or "seen it" persistence — opened only by clicking the button, every time (see spec: "Walkthrough is reachable at any time, not just once").
- No live binding to a real project's actual texture list, snapshot, or bake state — illustrative example content only.
- No change to `OverlayModBuilderProject`, `OverlayModBuilderStage`, `OverlayModBuilderService`, `TextureCompositor`, or `OverlayModWriter`.

## Decisions

### Generalize `DrawTutorialPopup()` into a shared stepper instead of duplicating it
Change its signature from the trigger-specific, no-argument form to `DrawTutorialPopup(string popupId, TutorialStep[] steps, ref int stepIndex)`, and update the Triggers tab's existing call site to pass `"Trigger Tutorial"`, its existing `TutorialSteps` array, and `ref tutorialStepIndex`. The new Overlay Mod Builder tab call passes `"Overlay Mod Builder Tutorial"`, a new step array, and a new `overlayTutorialStepIndex` field. `TutorialStep` (already a generic `Title`/`Body` pair, not trigger-specific) needs no change. The step count already comes from `steps.Length`, so a 4-step overlay tutorial needs no code change beyond its own content array.

**Alternative considered:** copy `DrawTutorialPopup()` into a second, overlay-specific method (as if it were a one-off, matching how the trigger tutorial was originally built standalone). Rejected — with a second consumer landing immediately, keeping two near-identical ~40-line steppers in the same file is the kind of duplication worth collapsing now rather than carrying forward; the generalized signature is a small, mechanical change to the one existing call site.

### Where the "Tutorial" button and popup call live
Mirror the Triggers tab exactly: a `Tutorial` button in `DrawOverlayModBuilderTab`, right-aligned opposite `Add Project` via `ImGui.GetContentRegionMax().X - buttonWidth` (the same idiom used for the Triggers tab's button), setting `overlayTutorialPopupShouldOpen = true` and `overlayTutorialStepIndex = 0`; a per-frame open check calling `ImGui.OpenPopup("Overlay Mod Builder Tutorial")`; and a call to the shared `DrawTutorialPopup(...)`.

### Step content: target texture, snapshot, stage, apply
Four static steps, matching the tab's actual sequential dependency order (a project has nothing to bake until a target is picked; nothing to stage until a snapshot exists; nothing to apply until at least one stage exists):
1. Target texture — what "base texture" means and where the picker's list comes from.
2. Snapshot — what capturing a snapshot does and why stages bake against it, not the live texture.
3. Stages — how a stage's overlay image composites onto the previous stage's own baked output, not the pristine snapshot.
4. Apply — what applying does (writes/registers a real Penumbra mod) and that "Create Trigger" is the natural next step once a project is applied.

**Alternative considered:** stop at 3 steps and fold "Apply" into the Stages step. Rejected — Apply is a distinct, easy-to-miss action (nothing happens automatically after adding a stage), and the trigger tutorial already established one concept per step as the pattern to match.

## Risks / Trade-offs

- **Changing `DrawTutorialPopup()`'s signature touches code shipped by the already-archived `trigger-editor-onboarding` change** → the edit is mechanical (add two parameters, thread them through the one existing call site) and behavior-preserving for the trigger tutorial; still worth a quick regression glance at the Triggers tab's tutorial while implementing, not just the new one.
- **The stage row already chains `Name` → filename text → `Browse...` → `Remove` via `SameLine`** — adding a help-marker there risks disturbing that row's spacing more than the flatter rows elsewhere in this pane → verify visually in-game specifically for the stage row, same caveat `trigger-editor-onboarding`'s design.md raised for its own busiest rows.
- **Static tutorial content can drift** if the base-texture-picker's source list or the stage-chaining model changes → same accepted trade-off as the trigger tutorial: illustrative/conceptual content degrades gracefully rather than becoming flatly wrong.

## Migration Plan

None needed — no persisted config shape changes, no version bump. Ships as a UI-only patch to `ConfigWindow.cs`; rollback is reverting that file.
