## Why

Live use surfaced three gaps in the overlay mod builder: removing a stage leaves its baked files behind on disk, so a newly-added stage can land on the same on-disk file a removed one used and show that stale content as its own preview before it's ever been baked; removing a project from ReactToMe never touches the mod Penumbra actually registered, leaving an orphaned mod in Penumbra's own list forever; and a generated mod always lands wherever Penumbra puts a freshly-registered mod, with no way to file it into one of the user's own mod-list folders (e.g. "Body").

## What Changes

- Each stage gets a permanent identity (a GUID) that its baked `.tex`/preview `.png` file names are derived from, instead of the stage's current position in the list. Removing a stage deletes that stage's own uniquely-named files immediately, and no later stage's file identity shifts as a result — a newly-added stage always starts with no baked file of its own, never inheriting another stage's leftover content.
- Removing a project SHALL delete its Penumbra mod too (via Penumbra's own `DeleteMod`), not just remove the project from ReactToMe's own list — but only after an explicit confirmation step, since this permanently deletes real files Penumbra manages and can't be undone.
- Add a per-project "Penumbra folder" field. Applying or recreating a project's mod SHALL set that mod's sort-order path in Penumbra (via `SetModPath`) to place it under the given folder (e.g. "Body") in Penumbra's own mod list, the same way the user could do by hand from Penumbra's UI.
- Add a "Create Trigger" button to a project's detail panel that generates a new trigger pre-populated with one `PenumbraStageThreshold` per baked stage (pointing at the project's mod/"Stages" group/each stage's option name), so escalating through a project's stages via a trigger doesn't require manually re-entering each stage by hand in the Triggers tab. The new trigger still needs its actual fire source (emote/chat phrase/job skill) configured manually — this button only wires up the Penumbra side.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `overlay-mod-builder`: stage file identity is now per-stage-permanent rather than position-based, so removing a stage cleans up its own files without disturbing any other stage; removing a project also deletes its Penumbra mod after confirmation; a project gains a configurable Penumbra mod-list folder; a project can generate a pre-configured trigger from its baked stages.

## Impact

- `ReactToMe/OverlayModBuilder/OverlayModBuilderProject.cs`: add a stable `Id` (Guid) to `OverlayModBuilderStage`; add a `PenumbraFolder` field to `OverlayModBuilderProject`.
- `ReactToMe/OverlayModBuilder/OverlayModWriter.cs`: `GetStageRelativeFilePath` keys off a stage's `Id` instead of its list index (another `StageFileSchemeVersion` bump, following the established pattern for on-disk scheme changes); `ApplyProject`'s per-stage loop and `Files` mapping follow the same change.
- `ReactToMe/OverlayModBuilder/OverlayModBuilderService.cs`: a new method to delete a stage's own baked/preview files when it's removed from a project; a new method to delete a project's Penumbra mod; apply/recreate additionally calls `SetModPath` when a project has a non-empty `PenumbraFolder`; a new method building a `ReactionTrigger` from a project's baked stages.
- `ReactToMe/Ipc/PenumbraIpc.cs`: new methods wrapping Penumbra's `DeleteMod(modDirectory, modName)` and `SetModPath(modDirectory, newPath, modName)`.
- `ReactToMe/Windows/ConfigWindow.cs`: stage removal calls the new cleanup method; project removal gains a confirmation step before deleting the Penumbra mod; a new "Penumbra folder" text field in the project detail panel; a "Create Trigger" button that adds the generated trigger and switches to the Triggers tab with it selected.
- No changes to trigger-firing behavior or the staged-Penumbra-mod reaction itself — this only adds a shortcut for authoring a `PenumbraStages` list, using the same fields a user would fill in by hand.
