## Purpose

Gives a newbie a guided, read-only walkthrough of the common trigger-building path — source, one reaction, duration — reachable on demand from the Triggers tab, without altering any trigger configuration.

## ADDED Requirements

### Requirement: Tutorial is reachable from the Triggers tab
The Triggers tab SHALL show a "Tutorial" control, separate from "Add Trigger", that opens a guided walkthrough on demand.

#### Scenario: Opening the tutorial
- **WHEN** the user selects the Tutorial control on the Triggers tab
- **THEN** a guided walkthrough opens

### Requirement: Walkthrough explains the common trigger-building path
The walkthrough SHALL present, in order, what a trigger's source is, how to pick a reaction, and what its duration/expiry controls — the path needed to build a typical trigger — using example values rather than any real trigger's fields.

#### Scenario: Walkthrough steps follow build order
- **WHEN** the user steps through the walkthrough from its start
- **THEN** it presents source guidance, then reaction guidance, then duration/expiry guidance, in that order

### Requirement: Walkthrough is read-only
The walkthrough SHALL NOT create, modify, or delete any trigger or configuration value, regardless of how far the user progresses through it or how it is closed.

#### Scenario: Completing the walkthrough leaves triggers unchanged
- **WHEN** the user completes every step of the walkthrough
- **THEN** the trigger list and every existing trigger's configuration are unchanged

#### Scenario: Closing the walkthrough early leaves triggers unchanged
- **WHEN** the user closes the walkthrough before completing it
- **THEN** the trigger list and every existing trigger's configuration are unchanged

### Requirement: Walkthrough excludes advanced configuration
The walkthrough SHALL NOT cover Penumbra staged escalation, revert-to-specific-design, or stacking/refresh behavior — that guidance is provided in the detail pane itself via its help-marker affordances instead.

#### Scenario: Advanced fields are not part of the walkthrough
- **WHEN** the user steps through the entire walkthrough
- **THEN** no step covers Penumbra staging, revert-to-design, or stacking/refresh configuration

### Requirement: Walkthrough is reachable at any time, not just once
The walkthrough SHALL remain reachable from the Triggers tab after being viewed, with no "seen it" state that hides or disables the control on subsequent visits.

#### Scenario: Reopening the walkthrough after viewing it
- **WHEN** the user has already completed the walkthrough once and selects the Tutorial control again
- **THEN** the walkthrough opens again from its first step
