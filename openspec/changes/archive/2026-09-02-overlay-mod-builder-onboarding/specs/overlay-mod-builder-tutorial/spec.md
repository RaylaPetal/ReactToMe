## Purpose

Gives a newbie a guided, read-only walkthrough of the common overlay-mod-builder project path — target texture, snapshot, one stage, apply — reachable on demand from the Overlay Mod Builder tab, without creating or altering any project.

## ADDED Requirements

### Requirement: Tutorial is reachable from the Overlay Mod Builder tab
The Overlay Mod Builder tab SHALL show a "Tutorial" control, separate from "Add Project", that opens a guided walkthrough on demand.

#### Scenario: Opening the tutorial
- **WHEN** the user selects the Tutorial control on the Overlay Mod Builder tab
- **THEN** a guided walkthrough opens

### Requirement: Walkthrough explains the common project-building path
The walkthrough SHALL present, in order, what a project's base/target texture is, what capturing a snapshot does, how a stage's overlay image builds on the previous stage, and what applying a project does — the path needed to build a typical project — using example values rather than any real project's fields.

#### Scenario: Walkthrough steps follow build order
- **WHEN** the user steps through the walkthrough from its start
- **THEN** it presents target-texture guidance, then snapshot guidance, then stage guidance, then apply guidance, in that order

### Requirement: Walkthrough is read-only
The walkthrough SHALL NOT create, modify, or delete any project, stage, or configuration value, and SHALL NOT call into Penumbra or trigger any bake/apply operation, regardless of how far the user progresses through it or how it is closed.

#### Scenario: Completing the walkthrough leaves projects unchanged
- **WHEN** the user completes every step of the walkthrough
- **THEN** the project list and every existing project's configuration are unchanged

#### Scenario: Closing the walkthrough early leaves projects unchanged
- **WHEN** the user closes the walkthrough before completing it
- **THEN** the project list and every existing project's configuration are unchanged

### Requirement: Walkthrough excludes advanced configuration
The walkthrough SHALL NOT cover a project's Priority, its Penumbra folder placement, or the recovery-oriented "Rebake All"/"Recreate Mod" actions — that guidance is provided in the project detail pane itself via its help-marker affordances instead.

#### Scenario: Advanced fields are not part of the walkthrough
- **WHEN** the user steps through the entire walkthrough
- **THEN** no step covers Priority, Penumbra folder, Rebake All, or Recreate Mod

### Requirement: Walkthrough is reachable at any time, not just once
The walkthrough SHALL remain reachable from the Overlay Mod Builder tab after being viewed, with no "seen it" state that hides or disables the control on subsequent visits.

#### Scenario: Reopening the walkthrough after viewing it
- **WHEN** the user has already completed the walkthrough once and selects the Tutorial control again
- **THEN** the walkthrough opens again from its first step
