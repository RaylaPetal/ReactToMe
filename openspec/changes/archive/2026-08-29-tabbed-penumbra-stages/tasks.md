## 1. Tabbed Penumbra stages UI

- [x] 1.1 Rework `DrawPenumbraStages` in `ReactToMe/Windows/ConfigWindow.cs` to render stages inside `ImGui.BeginTabBar`/`ImGui.BeginTabItem`, one tab per stage in `trigger.PenumbraStages` order, with each tab's label showing that stage's fire-count threshold (e.g. `"Stage (5)"`); move that stage's existing fields (threshold input, mod picker, option group combo, option combo) into its tab body unchanged; verify by configuring a trigger with 3 stages and confirming each stage's fields appear in its own tab, switching tabs shows only that stage's fields, and edits still persist via `configuration.Save()`
- [x] 1.2 Replace the per-stage inline "Remove" button with a per-tab close mechanism (e.g. `ImGuiTabItemFlags.NoCloseWithMiddleMouseButton` unset plus an explicit close button in the tab, or a "Remove this stage" control inside the tab body), removing that stage from `trigger.PenumbraStages` and calling `configuration.Save()`; verify removing a tab drops that stage and an adjacent tab becomes selected when others remain
- [x] 1.3 Verify the "Add Penumbra stage" button still appends a new stage (defaulting threshold/mod/group from the last stage, as today) and that the new stage's tab is shown; verify a trigger with zero stages shows no tab bar, only the add control
- [x] 1.4 Build and confirm 0 warnings/0 errors with `dotnet build ReactToMe/ReactToMe.csproj -c Debug -p:Platform=x64`

## 2. End-to-end verification

- [ ] 2.1 Manually verify the scenario groups in `specs/trigger-config-ui/spec.md`'s new "Penumbra stages are presented as tabs" requirement: multiple stages show as separate tabs labeled by threshold, adding a stage adds a tab, removing the open tab selects another, and no stages shows no tab bar
