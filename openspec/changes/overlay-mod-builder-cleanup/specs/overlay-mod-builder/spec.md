## ADDED Requirements

### Requirement: Removing a stage deletes its own baked files and never affects another stage's identity
A stage SHALL have a permanent identity that its baked files are derived from, independent of its position in the project's stage list. Removing a stage SHALL delete that stage's own baked texture and preview files. Removing a stage SHALL NOT change which files any other stage's baked output lives at, and a newly-added stage SHALL NOT show or reuse another stage's leftover baked files before it has been baked itself.

#### Scenario: Removing a stage deletes its files
- **WHEN** the user removes a stage that has already been baked
- **THEN** that stage's baked texture and preview files are deleted

#### Scenario: A new stage never inherits a removed stage's leftover file
- **WHEN** the user removes a stage and then adds a new stage
- **THEN** the new stage shows no preview until it is baked itself, never a previously-removed stage's stale content

#### Scenario: Removing a middle stage does not disturb stages after it
- **WHEN** a project has three baked stages and the user removes the middle one
- **THEN** the remaining stages' own baked files and preview thumbnails are unaffected

### Requirement: Removing a project also deletes its Penumbra mod, after confirmation
Removing a project that has been applied at least once SHALL delete the corresponding mod from Penumbra, in addition to removing the project from ReactToMe's own list. Because this permanently deletes files Penumbra manages, the system SHALL require an explicit confirmation step before deleting, separate from the initial removal action.

#### Scenario: Removing an applied project deletes its Penumbra mod after confirming
- **WHEN** the user removes a project that has an applied Penumbra mod, and confirms the deletion
- **THEN** the project is removed from ReactToMe and its mod no longer appears in Penumbra's mod list

#### Scenario: Removal without confirmation does not delete the Penumbra mod
- **WHEN** the user starts removing a project but does not complete the confirmation step
- **THEN** neither the project nor its Penumbra mod is deleted

#### Scenario: Removing a never-applied project needs no Penumbra cleanup
- **WHEN** the user removes a project that was never applied
- **THEN** the project is removed from ReactToMe without any Penumbra deletion step

### Requirement: A project's mod can be placed in a Penumbra mod-list folder
A project SHALL have a configurable folder path. Applying or recreating the project's mod SHALL set that mod's sort-order path in Penumbra to place it under the configured folder in Penumbra's own mod list, equivalent to setting it by hand from Penumbra's UI.

#### Scenario: Applying a project with a configured folder files the mod under it
- **WHEN** a project with a configured Penumbra folder is applied
- **THEN** Penumbra's mod list shows the generated mod filed under that folder

#### Scenario: An empty folder leaves the mod's placement untouched
- **WHEN** a project has no configured folder
- **THEN** applying it does not change the mod's existing placement in Penumbra's mod list

### Requirement: A project can generate a pre-configured trigger from its stages
A project SHALL offer an action that creates a new trigger whose staged-Penumbra-mod reaction has one threshold per the project's baked stages, targeting the project's own generated mod, its "Stages" option group, and each stage's own option name, in ascending threshold order. The generated trigger's fire source (what emote, chat phrase, or job skill fires it) SHALL remain unconfigured, to be set by the user afterward.

#### Scenario: Generating a trigger populates one threshold per baked stage
- **WHEN** the user generates a trigger from a project with three baked stages
- **THEN** the new trigger's staged-Penumbra-mod reaction has three thresholds, one per stage, in ascending order, all targeting the project's own mod and "Stages" group

#### Scenario: An unbaked stage is skipped
- **WHEN** the user generates a trigger from a project that has a stage with no baked file
- **THEN** that stage is left out of the generated trigger's thresholds, the same way it would be left out of the mod itself when applied
