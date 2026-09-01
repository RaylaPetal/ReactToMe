## 1. Penumbra IPC additions

- [x] 1.1 Add `PenumbraIpc.DeleteMod(string modDirectory, string modName)`, wrapping `DeleteMod.Invoke(modDirectory, modName)`, following the existing `IsAcceptable(PenumbraApiEc)` result-checking pattern. Named `DeleteGeneratedMod` to match this file's existing `AddGeneratedMod`/`ReloadGeneratedMod` naming convention.
- [x] 1.2 Add `PenumbraIpc.SetModPath(string modDirectory, string newPath, string modName)`, wrapping `SetModPath.Invoke(modDirectory, newPath, modName)` — note the reordered/defaulted argument positions on the actual IPC subscriber's `Invoke`, not the `IPenumbraApiMods` interface's declared order.
- [x] 1.3 Build and confirm 0 warnings/0 errors.

## 2. Stable per-stage file identity

- [x] 2.1 Add `Id` (Guid, default `Guid.NewGuid()`) to `OverlayModBuilderStage`.
- [x] 2.2 Change `OverlayModWriter.GetStageRelativeFilePath` to take a `Guid stageId` instead of an `int stageIndex`, and derive the file name from it (e.g. `chara/reacttome_overlay_stage_{stageId:N}.tex`). Bump `StageFileSchemeVersion`.
- [x] 2.3 Update every call site (`OverlayModBuilderService.BakeStageAsync`, `GetStagePreviewPath`, `OverlayModWriter.ApplyProject`'s per-stage loop) to pass the stage's own `Id` instead of a list/loop index. Chain-order lookups (which stage is the immediate predecessor) still use list position — only file naming changes. Also found and fixed the same index-based-identity flaw in `ConfigWindow.ImportOverlayImage` (the copy of the user's imported overlay source image was also named `{projectId}-stage{index}`, not in the task's original description but the identical root cause) — now keyed on `stage.Id` too.
- [x] 2.4 Build and confirm 0 warnings/0 errors.
- [x] 2.5 Found live while testing: after the `StageFileSchemeVersion` bump above made every already-baked stage stale, adding an overlay image to a later stage (e.g. Stage 7) failed with "needs the previous stage baked first" — the per-stage "Browse..." handler only ever baked the one stage just edited, in isolation, so it had no way to also re-bake earlier stages (5, 6) that hadn't been touched and were themselves stale under the new scheme (or, more generally, ones that never got a chance to bake because the user filled in stages out of chain order). Fixed by extracting the existing chain-order "bake everything that needs it" loop from the shared apply path into `OverlayModBuilderService.BakeStagesThroughAsync(project, forceRebakeAll, throughIndex)`, and adding `BakeStageAndChainAsync(project, stage)` — which the UI's "Browse..." handler now calls instead of baking the single stage directly — so picking an image for any stage bakes every not-yet-current stage before it too, in order, not just itself.

## 3. Stage removal cleanup

- [x] 3.1 Add a method (e.g. `OverlayModBuilderService.DeleteStageFiles(project, stage)`) that deletes that stage's baked `.tex` and preview `.png` if they exist — a missing file is a no-op, not an error.
- [x] 3.2 Call it from `ConfigWindow`'s stage "Remove" button handler before removing the stage from `project.Stages`.
- [x] 3.3 Build and confirm 0 warnings/0 errors.

## 4. Project removal deletes the Penumbra mod, with confirmation

- [x] 4.1 Add `OverlayModBuilderService.DeleteModAsync(project)` (or similar), calling `penumbraIpc.DeleteMod` with the project's mod directory name and display name — only meaningful when `project.IsApplied`. Implemented as synchronous `DeleteMod(project)` (no `Async` suffix) since `PenumbraIpc.DeleteGeneratedMod` itself is synchronous — matches this file's existing `SetModPriority` call, which is also sync.
- [x] 4.2 In `ConfigWindow`, change "Remove Project" into a two-click confirm flow when `project.IsApplied` (first click shows a "Confirm Remove (deletes Penumbra mod too)" state; second click actually deletes the mod via 4.1, then removes the project from `configuration.OverlayModBuilderProjects`). An unapplied project still removes immediately on the first click, with no Penumbra call.
- [x] 4.3 Build and confirm 0 warnings/0 errors.

## 5. Penumbra mod-list folder

- [x] 5.1 Add `PenumbraFolder` (string, default empty) to `OverlayModBuilderProject`.
- [x] 5.2 In the shared apply path (`OverlayModBuilderService`'s private `ApplyProjectAsync`), after a successful `AddGeneratedMod`/`ReloadGeneratedMod`, call `penumbraIpc.SetModPath` with `$"{project.PenumbraFolder}/{project.DisplayName}"` when `project.PenumbraFolder.Length > 0` — mirroring the existing `project.Priority != 0` guard already used for `SetModPriority`.
- [ ] 5.3 **STOP for live confirmation**: apply a project with a folder set (e.g. "Body"), confirm in Penumbra's own UI that the mod is actually filed under that folder with the expected display name. Do not consider this task done without this check, since `SetModPath`'s exact expected path format was inferred from its doc comment, not verified live.
- [x] 5.4 Add a "Penumbra folder" text field to `ConfigWindow`'s project detail panel, bound to `project.PenumbraFolder`.
- [x] 5.5 Build and confirm 0 warnings/0 errors.

## 6. Create Trigger from project

- [x] 6.1 Add a method (e.g. `OverlayModBuilderService.BuildTriggerFromProject(project)`) that builds a `ReactionTrigger` with `PenumbraReactionMode = Staged` and one `PenumbraStageThreshold` per baked stage (`ModDirectory = OverlayModWriter.GetModDirectoryName(project.Id)`, `ModName = project.DisplayName`, `OptionGroupName = "Stages"`, `OptionName = stage.Name`, `Threshold` = 1-based ascending position among baked stages) — skipping any stage with no baked file, the same way `OverlayModWriter.ApplyProject` already does when writing the mod's own options.
- [x] 6.2 Add a "Create Trigger" button to `ConfigWindow`'s project detail panel: builds the trigger via 6.1, adds it to `configuration.Triggers`, saves, and switches to the Triggers tab with the new trigger selected.
- [x] 6.3 Build and confirm 0 warnings/0 errors.
- [x] 6.4 Found live while testing: the new trigger's Penumbra stage picker in the Triggers tab didn't recognize the project's own mod, since `ConfigWindow`'s cached `penumbraMods`/`penumbraModSettingsCache` (populated once via "Refresh lists") predate a mod applied earlier in the same session — the user had to manually click "Refresh lists" before "Create Trigger" for it to work. Fixed by calling `RefreshLists()` as part of the "Create Trigger" button handler itself, so the Triggers tab always has current mod data by the time it switches there.

## 7. End-to-end verification

- [ ] 7.1 Manually verify the full scenario set in `specs/overlay-mod-builder/spec.md`'s added requirements: removing a baked stage deletes its files and a newly-added stage shows no stale preview; removing a middle stage doesn't disturb the stages after it; removing an applied project requires confirmation and then deletes the mod from Penumbra's own list; removing a never-applied project needs no confirmation; a configured Penumbra folder actually files the mod there on apply; generating a trigger from a project with baked stages produces the expected thresholds and lands on the Triggers tab with it selected.
