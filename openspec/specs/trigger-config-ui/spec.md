# trigger-config-ui Specification

## Purpose

Lets the config screen scale to a growing number of trigger fields by presenting triggers as a filterable master-detail list with sectioned, tooltipped fields, instead of one flat stack of controls per trigger.

## Requirements

### Requirement: Triggers are presented as a filterable list with a detail pane
The config screen SHALL present the list of triggers as a filterable list, with the currently selected trigger's fields shown in a separate detail pane. Selecting a different trigger in the list SHALL show that trigger's fields in the detail pane.

#### Scenario: Filtering narrows the list
- **WHEN** the user types text into the trigger list's filter
- **THEN** only triggers whose label matches the filter remain visible in the list

#### Scenario: Selecting a trigger shows its fields
- **WHEN** the user selects a trigger in the list
- **THEN** the detail pane shows that trigger's fields, replacing whatever was shown for the previously selected trigger

### Requirement: Each trigger has a list label
Each trigger SHALL have a label shown in the list: its configured display name if set, otherwise a label generated from its source type and configuration (its emote's name, its chat phrase, or its job skill's name).

#### Scenario: Custom name takes priority
- **WHEN** a trigger has a display name configured
- **THEN** its list label is that name

#### Scenario: Falls back to a generated label
- **WHEN** a trigger has no display name configured
- **THEN** its list label is generated from its source type and configuration

### Requirement: Detail pane fields are grouped into labeled sections
The detail pane SHALL group a trigger's fields into labeled sections separating its source configuration, its reactions, and its timing/revert configuration, rather than presenting all fields as one undifferentiated list.

#### Scenario: Fields are grouped, not flat
- **WHEN** the detail pane is shown for any trigger
- **THEN** its source fields, reaction fields, and timing/revert fields appear under their own distinct labeled sections

### Requirement: Fields offer hover guidance
Fields in the detail pane whose purpose or behavior is not obvious from their label SHALL offer hover guidance explaining them.

#### Scenario: Hovering a non-obvious field shows guidance
- **WHEN** the user hovers a field that isn't self-explanatory from its label alone
- **THEN** guidance explaining that field is shown

### Requirement: Settings and active effects are tabs alongside triggers, not separate windows
The plugin SHALL present exactly one window, with plugin-wide settings and the live list of active effects each as their own tab alongside the triggers tab, rather than requiring a separate window for either.

#### Scenario: Settings reachable without a new window
- **WHEN** the user wants to change a plugin-wide setting
- **THEN** they switch to the settings tab of the same window, without opening another window

#### Scenario: Active effects reachable without a new window
- **WHEN** the user wants to see which effects are currently active or force-revert them
- **THEN** they switch to the active-effects tab of the same window, without opening another window

### Requirement: Job-skill source picks a job before a skill
When configuring a job-skill source, the config screen SHALL first offer a choice of job, then narrow the skill picker to that job's own skills — not one flat list of every action in the game.

#### Scenario: Skill picker is scoped to the chosen job
- **WHEN** the user selects a job for a job-skill trigger's source
- **THEN** the skill picker offers only that job's own actions

#### Scenario: No job selected yet shows no skill picker
- **WHEN** a job-skill trigger has no job selected yet
- **THEN** no skill picker is shown

### Requirement: Penumbra stages are presented as tabs
When a trigger's Reactions section has Penumbra stages configured, the config screen SHALL present them as a tab bar of per-stage tabs — one tab per stage, showing that stage's own fields — rather than a stacked list of full stage blocks. Each tab's label SHALL show that stage's fire-count threshold so stages remain identifiable without opening them.

#### Scenario: Multiple stages appear as separate tabs
- **WHEN** a trigger has three Penumbra stages configured
- **THEN** the Reactions section shows three tabs, each labeled with its stage's fire-count threshold, and only the selected tab's fields are shown at once

#### Scenario: Adding a stage adds a tab
- **WHEN** the user adds a new Penumbra stage to a trigger
- **THEN** a new tab for that stage appears, labeled with its threshold

#### Scenario: Removing the open stage's tab selects another
- **WHEN** the user removes the currently open stage's tab and at least one other stage remains
- **THEN** an adjacent stage's tab becomes selected

#### Scenario: No stages configured shows no tab bar
- **WHEN** a trigger has no Penumbra stages configured
- **THEN** no stage tab bar is shown, only the control to add a first stage

### Requirement: Penumbra reaction chooses a mode
A trigger's Penumbra reaction SHALL have exactly one mode: None, Single, or Staged. The Reactions section's Penumbra editor SHALL show only the fields relevant to the selected mode: None shows just the mode choice and no mod/option fields; Single shows a flat mod picker plus whatever option fields that mod's group count calls for (see "Mod option fields adapt to how many option groups a mod has") with no fire-count threshold field and no tab bar; Staged shows the existing tabbed, threshold-driven editor.

#### Scenario: None mode shows no mod fields
- **WHEN** a trigger's Penumbra reaction mode is None
- **THEN** the Reactions section shows only the mode choice, with no mod, option-group, option, or threshold fields

#### Scenario: Single mode shows flat fields with no threshold
- **WHEN** a trigger's Penumbra reaction mode is Single
- **THEN** the Reactions section shows a mod picker and that mod's option fields (per its group count) with no fire-count threshold field and no tab bar

#### Scenario: Staged mode shows the tabbed threshold editor
- **WHEN** a trigger's Penumbra reaction mode is Staged
- **THEN** the Reactions section shows the tabbed stage editor, with each stage's own threshold, mod, and option fields (per its group count), exactly as already specified for staged Penumbra editing

### Requirement: Mod option fields adapt to how many option groups a mod has
Not every Penumbra mod has option groups, and not every mod with options has more than one group. The mod-option fields (used by both Single mode and each Staged-mode stage) SHALL adapt to the selected mod's actual group count: a mod with no option groups SHALL show no option-group or option fields at all; a mod with exactly one option group SHALL show only that group's option picker, without a separate option-group selector; a mod with two or more option groups SHALL show an option-group selector followed by the selected group's option picker.

#### Scenario: A mod with no option groups shows no option fields
- **WHEN** the selected mod has zero option groups
- **THEN** no option-group selector or option picker is shown for it

#### Scenario: A mod with exactly one option group skips the group selector
- **WHEN** the selected mod has exactly one option group
- **THEN** only that group's option picker is shown, with no separate option-group selector

#### Scenario: A mod with multiple option groups shows both selectors
- **WHEN** the selected mod has two or more option groups
- **THEN** an option-group selector is shown, and selecting a group reveals its own option picker

### Requirement: Switching Penumbra reaction mode preserves compatible configuration
When the user switches a trigger's Penumbra reaction mode, the system SHALL carry over configuration that remains meaningful in the new mode and discard what does not.

#### Scenario: Single to Staged keeps the entry and reveals its threshold
- **WHEN** a trigger's mode changes from Single (with a mod/option configured) to Staged
- **THEN** that mod/option becomes the first stage, and its fire-count threshold field becomes visible and editable

#### Scenario: Staged to Single keeps only the first stage
- **WHEN** a trigger's mode changes from Staged (with multiple stages configured) to Single
- **THEN** only the first stage's mod and option are kept, and any other stages are discarded

#### Scenario: Switching to None clears the configured mod reaction
- **WHEN** a trigger's mode changes to None
- **THEN** any previously configured mod, option-group, option, and threshold values for this trigger's Penumbra reaction are cleared

### Requirement: Pre-existing Penumbra stage configuration loads as Staged mode
A trigger whose Penumbra stages were configured before this mode selector existed SHALL be interpreted as Staged mode on load, so its existing behavior and presentation are unchanged after upgrading.

#### Scenario: A trigger with existing stages opens in Staged mode
- **WHEN** a trigger configured before this change has one or more Penumbra stages already set up
- **THEN** it loads with its Penumbra reaction mode as Staged, showing the same tabbed editor and applying the same matching/escalation behavior as before this change
