## Purpose

Lets a reaction trigger drive Penumbra mods through user-defined fire-count thresholds that escalate as the trigger repeatedly fires, instead of only ever applying a single fixed state.

## ADDED Requirements

### Requirement: Trigger can configure threshold-based Penumbra stages
A reaction trigger SHALL offer an optional set of staged Penumbra entries. Each entry independently specifies a fire-count threshold (1-20), a Penumbra mod, one of that mod's option groups, and one of that group's option names — entries are not required to share the same mod or option group. This action is independent of the trigger's Glamourer design, Moodle, and chat message actions — any combination of them may be configured together. Threshold values are chosen freely by the user; they need not be evenly spaced or start at 1.

#### Scenario: Trigger has no staged entries configured
- **WHEN** a trigger has no Penumbra stage entries
- **THEN** firing it has no effect on any Penumbra mod

#### Scenario: Trigger has staged entries configured
- **WHEN** a trigger has at least one stage entry, each specifying its own mod, option group, option, and threshold
- **THEN** firing it can enable a stage's mod and set its option group per the requirements below

#### Scenario: Stages may target different mods
- **WHEN** a trigger's stage entries name more than one distinct mod (or more than one option group within the same mod)
- **THEN** the system honors each entry's own mod/group/option independently, rather than assuming all stages share one mod

### Requirement: Repeat fires increase a fire count, capped at 20
Each time a trigger with staged entries configured fires while its effect is already active (and it is not configured to stack multiple instances), the system SHALL increase that trigger's fire count by one, up to a maximum of 20 — matching FFXIV's native maximum debuff stack count. Firing again once the fire count is already 20 SHALL leave it at 20.

#### Scenario: First fire starts the fire count at 1
- **WHEN** a trigger fires for the first time
- **THEN** its fire count is 1

#### Scenario: Repeat fire increases the fire count
- **WHEN** a trigger's fire count is 6 and it fires again
- **THEN** its fire count becomes 7

#### Scenario: Fire count caps at 20
- **WHEN** a trigger's fire count is already 20 and it fires again
- **THEN** its fire count remains 20

### Requirement: The active stage is the highest threshold reached
At any fire count, the system SHALL treat the active stage as the configured entry with the greatest threshold less than or equal to the current fire count. If the fire count has not yet reached any configured threshold, no stage is active. Whenever the resolved stage differs from what's currently applied, the system SHALL enable that stage's mod (if not already enabled) and set its option group to that stage's option, via Penumbra's IPC. A fire that does not change the resolved stage SHALL NOT re-issue that call.

#### Scenario: Fire count below the lowest threshold applies nothing yet
- **WHEN** a trigger's stages are configured with thresholds 5, 10, and 15, and its fire count is 3
- **THEN** no stage has been applied for this trigger yet

#### Scenario: Fire count reaching a threshold applies its stage
- **WHEN** a trigger's stages are configured with thresholds 1, 5, and 10, and its fire count reaches 5
- **THEN** the mod/option group/option configured at threshold 5 is applied

#### Scenario: Fire count between thresholds holds the last-reached stage
- **WHEN** a trigger's stages are configured with thresholds 1, 5, and 10, and its fire count is 7
- **THEN** the stage configured at threshold 5 remains applied, since fire count 7 has not reached threshold 10

### Requirement: Switching to a stage with a different mod disables the previous one
When the resolved stage changes and its mod differs from the previously active stage's mod, the system SHALL disable the previously active mod before enabling and setting the new stage's mod. When the resolved stage changes but its mod is the same as before (only the option group or option differs), the system SHALL NOT disable that mod — it stays enabled throughout.

#### Scenario: Escalating within the same mod does not disable it
- **WHEN** a trigger's fire count crosses a threshold into a new stage that uses the same mod as the previously active stage (a different option group, option, or both)
- **THEN** that mod remains enabled throughout, and only its option group is updated

#### Scenario: Escalating to a different mod disables the previous one
- **WHEN** a trigger's fire count crosses a threshold into a new stage that names a different mod than the previously active stage
- **THEN** the previously active mod is disabled before the new stage's mod is enabled and set

### Requirement: Timer expiry resets the currently active stage
When a trigger's timer expires (or its effects are manually force-reverted), the system SHALL disable whichever mod is currently active for that trigger's stage, in addition to any existing Glamourer revert behavior. A trigger configured to never expire SHALL keep its currently active stage enabled indefinitely, the same as it keeps its other effects, until manually cleared.

#### Scenario: Expiry disables the active stage's mod
- **WHEN** a trigger with an active staged entry expires
- **THEN** that stage's mod is disabled

#### Scenario: No-expiration trigger keeps its stage
- **WHEN** a trigger configured with no expiration reaches a stage and no manual clear happens
- **THEN** that stage's mod remains enabled indefinitely

### Requirement: Stage progress is independent per trigger
Each trigger's fire count SHALL be tracked independently. Firing one trigger SHALL NOT affect another trigger's fire count, even if both are configured with staged entries.

#### Scenario: Two different triggers stage independently
- **WHEN** trigger A has a fire count of 6 and trigger B (a different trigger, also staged) fires for the first time
- **THEN** trigger A's fire count remains 6 and trigger B's fire count is 1
