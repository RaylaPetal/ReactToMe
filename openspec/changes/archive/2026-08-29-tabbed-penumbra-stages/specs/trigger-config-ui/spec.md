## ADDED Requirements

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
