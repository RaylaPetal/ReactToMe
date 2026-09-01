## Why

Live use of the just-shipped `overlay-mod-builder` tool surfaced four gaps: every stage bakes independently against the same pristine base, so a stage meant to build on a previous stage's look (e.g. progressively adding tattoos) has to redraw everything from scratch instead of layering; there's no baseline "no overlay" option in the generated mod, so reverting to the plain base texture means disabling the mod entirely rather than picking an option; deleting the generated mod from within Penumbra directly leaves the project stuck (re-applying tries to reload a mod that no longer exists) with no way to recover from the UI; and the generated mod always take whatever priority Penumbra assigns a newly-registered mod, so it can silently lose file-redirect conflicts against another mod touching the same Bibo base texture.

## What Changes

- Stages become chained: capturing a project's snapshot generates an implicit, non-removable "Stage 0" baked directly from that pristine snapshot with no overlay. Every stage after that composites its own overlay on top of the *previous stage's own baked output* (Stage 1 on Stage 0, Stage 2 on Stage 1, etc.) instead of every stage independently compositing against the same pristine base. Recapturing the snapshot invalidates Stage 0 and, transitively, every stage after it, forcing a full re-bake down the chain in order.
- Add a "Recreate Mod" action per project that re-writes the mod folder from scratch and re-registers it with Penumbra (`AddMod`), for recovering a project whose generated mod was deleted directly from Penumbra's own UI — normal "Apply" alone cannot recover from this, since it assumes the mod is still registered once a project has applied successfully once.
- Add a per-project priority field, written to Penumbra via a new `TrySetModPriority` IPC call against the local player's active collection whenever the project is applied or recreated — this is Penumbra's own inter-mod priority, deciding which mod wins when more than one enabled mod redirects the same file (e.g. two overlay projects, or another body mod, both touching `chara/bibo_mid_base.tex`), not anything stored inside the generated mod's own `meta.json`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `overlay-mod-builder`: stages composite in a chain against the previous stage's own output rather than all independently against one pristine snapshot; an implicit "Stage 0" baseline option is always generated from the pristine snapshot; a project gains a "Recreate Mod" recovery action and a per-project Penumbra mod priority.

## Impact

- `ReactToMe/OverlayModBuilder/OverlayModBuilderProject.cs`: stages need to track their own baked-output path (to serve as the *next* stage's composite base) instead of only tracking staleness flags; add a `Priority` field to the project; the implicit Stage 0 concept needs modeling (either a real `OverlayModBuilderStage` the UI treats as non-removable, or a distinct field — decide in design.md).
- `ReactToMe/OverlayModBuilder/OverlayModBuilderService.cs`: `BakeStageAsync` needs to composite against the previous stage's baked file instead of always the pristine snapshot; recapturing the snapshot needs to invalidate the whole chain, not just versions that already feed into today's per-stage staleness check; add `RecreateModAsync`; apply/recreate need to call the new priority IPC after writing the mod.
- `ReactToMe/Ipc/PenumbraIpc.cs`: new method wrapping Penumbra's `TrySetModPriority(collectionId, modDirectory, modName, priority)`, resolving the player's active collection the same way `GetCollectionForObject` is already used elsewhere in this file.
- `ReactToMe/Windows/ConfigWindow.cs`: show the implicit Stage 0 baseline (read-only preview, no image picker, no remove button) ahead of the editable stage list; add a "Recreate Mod" button; add a priority input field.
- No changes to trigger-firing behavior or the staged-Penumbra-mod reaction — this only changes how a project's own mod is built and what priority it's registered with.
