## 1. Help-marker helper

- [x] 1.1 Add a private `DrawHelpMarker(string tooltip)` method to `ConfigWindow.cs` following the existing `Penumbra reaction` idiom (`ImGui.SameLine()` + `ImGui.TextDisabled("(?)")` + `IsItemHovered()`/`SetTooltip(tooltip)`), and verify `dotnet build ReactToMe/ReactToMe.csproj -c Debug -p:Platform=x64` succeeds with the new method compiling cleanly.

## 2. Discoverable guidance in the trigger detail pane

- [x] 2.1 In `DrawTriggerDetail` and `DrawSourceSection`, call `DrawHelpMarker` with each field's existing tooltip text for: Display name, Trigger type combo, Chat phrase field, Who can say it combo, Skill picker, Character name filter — removing their now-redundant bare `IsItemHovered`/`SetTooltip` calls. Verify in-game that each field shows a `(?)` whose hover text matches what it showed before this change.
- [x] 2.2 In `DrawReactionsSection`, call `DrawHelpMarker` for: Chat message field, Gesture picker, Chat/gesture cooldown field — same removal-and-replace as 2.1. Verify in-game the same way.
- [x] 2.3 In `DrawTimingSection`, move Duration's guidance onto the `"Duration"` label via `DrawHelpMarker`, removing the `IsItemHovered` check currently placed after the seconds `InputInt` (which only ever fires for that sub-field). Verify in-game that hovering the `Duration` text itself — not just the seconds box — shows the tooltip.
- [x] 2.4 Add new `DrawHelpMarker` calls with new tooltip text for `Refresh timer on repeat` (explains it restarts the timer on a repeat fire instead of leaving the original countdown running) and `Stack multiple` (explains it allows more than one concurrent instance instead of the normal single-slot replace-on-restack behavior). Verify in-game both checkboxes now show a `(?)` with this guidance.
- [x] 2.5 In `DrawTimingSection`, call `DrawHelpMarker` for the `On expiry` combo and `No expiration` checkbox, replacing their existing bare `IsItemHovered`/`SetTooltip` calls. Verify in-game the same way as 2.1.
- [x] 2.6 Add `DrawHelpMarker` calls for the fields under "Penumbra reaction" that had no guidance at all — the `Mode` combo, a stage's `Fire count` field, and a stage's `Mod`, `Option group`, and `Option` pickers — plus the `Revert-to design` field in `DrawTimingSection`, which likewise had none. Verify in-game that each shows a `(?)` with guidance.

## 3. Tutorial walkthrough

- [x] 3.1 Add `tutorialPopupShouldOpen` (bool) and `tutorialStepIndex` (int) fields to `ConfigWindow`, and a `Tutorial` button in `DrawTriggersTab`, right-aligned opposite `Refresh lists`/`Add Trigger`, that sets `tutorialPopupShouldOpen = true` and resets `tutorialStepIndex = 0`. Verify by building and confirming the button renders in-game on the Triggers tab.
- [x] 3.2 In `DrawTriggersTab`, add the per-frame open check (`if (tutorialPopupShouldOpen) { ImGui.OpenPopup("Trigger Tutorial"); tutorialPopupShouldOpen = false; }`) and a call to a new `DrawTutorialPopup()` method, mirroring `DrawOverlayPreviewPopup`'s `BeginPopupModal`/`EndPopup` shape. Verify in-game that clicking `Tutorial` opens a modal titled "Trigger Tutorial".
- [x] 3.3 Implement `DrawTutorialPopup()` with a static, hardcoded 3-entry step array — Source, Reaction, Timing — each with a title and explanatory body using illustrative example values (not `configuration.Triggers` or any live trigger data), rendering the entry at `tutorialStepIndex`. Verify in-game that opening the tutorial shows the Source step first.
- [x] 3.4 Add `Back`/`Next` buttons that clamp `tutorialStepIndex` to `[0, stepCount)`, and a `Done` button on the final step that calls `ImGui.CloseCurrentPopup()`. Verify in-game that stepping through goes Source -> Reaction -> Timing in order, `Back` is disabled/absent on the first step, `Next` is disabled/absent (replaced by `Done`) on the last step, and both `Done` and the popup's own close control dismiss it.
- [x] 3.5 Confirm `DrawTutorialPopup` and its step content never reference `configuration.Triggers`, `selectedTriggerId`, or call `configuration.Save()` (code review / grep for those identifiers within the new method), and verify in-game that the trigger list and every trigger's fields are unchanged before and after opening, stepping through, and closing the tutorial.
- [x] 3.6 Verify in-game that reopening the tutorial after previously reaching its last step and closing it starts over from the Source step, not the last-viewed step.

## 4. Final verification

- [x] 4.1 Run `dotnet build ReactToMe/ReactToMe.csproj -c Debug -p:Platform=x64` and confirm it completes with 0 errors and 0 warnings.
- [x] 4.2 Load the built plugin in-game and manually verify: every field touched in tasks 2.1-2.5 shows a `(?)` with correct guidance on hover; the `Tutorial` button opens, steps through, and closes correctly; existing trigger workflows (Add Trigger, editing fields, Remove Trigger, Test Fire) are unaffected.
