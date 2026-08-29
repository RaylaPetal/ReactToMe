## Purpose

Lets the config screen scale to a growing number of trigger fields by presenting triggers as a filterable master-detail list with sectioned, tooltipped fields, instead of one flat stack of controls per trigger.

## ADDED Requirements

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
