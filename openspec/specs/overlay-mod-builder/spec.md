# overlay-mod-builder Specification

## Purpose

Lets the user author a permanent, multi-stage Penumbra mod from imported overlay images, baked against a pristine snapshot of a chosen character texture, so the existing staged-Penumbra-mod trigger reaction can escalate through them exactly like any other pre-built mod.

## Requirements

### Requirement: A dedicated tab hosts mod-authoring, separate from trigger configuration
The config screen SHALL offer a top-level tab, alongside Active Effects / Triggers / Settings, dedicated to authoring overlay-mod-builder projects. This workflow is independent of configuring any trigger — a project's stages are plain Penumbra mod options once applied, not something a trigger references directly by project.

#### Scenario: The tab is reachable without opening a trigger
- **WHEN** the user wants to create or edit an overlay-mod-builder project
- **THEN** they switch to its own tab, without needing to open any trigger's configuration

### Requirement: Base texture is picked from a combined list of live and declared paths whose immediate parent directory is "chara"
A project's base texture — what its pristine snapshot reads pixel data from — SHALL be chosen from a searchable list combining two sources, both kept only where the game path's immediate parent directory is named `chara` (e.g. `chara/bibo_mid_base.tex`, or an absolute path like `Z:\...\Skin Overlay Kaede\chara\kaede overlay.tex` — what precedes `chara` doesn't matter): every texture Penumbra currently reports as actively resolving for the local player, and every installed mod's own declared file redirect read directly from that mod's files on disk (regardless of whether the mod is currently enabled). This pick is independent of what a generated mod actually redirects (see below).

#### Scenario: Picker reflects both live-resolving and mod-declared chara/ paths
- **WHEN** the base-texture picker is opened
- **THEN** it lists every `chara/<file>`-shaped path Penumbra currently reports as resolving for the local player, plus every `chara/<file>`-shaped redirect declared by any installed mod's own files, deduplicated

#### Scenario: Vanilla nested paths and unrelated equipment textures are excluded
- **WHEN** a texture (live-resolving or mod-declared) sits under vanilla's own deeply-nested `chara/human/.../obj/<part>/...` paths, where the immediate parent is the part name rather than `chara` itself
- **THEN** it does not appear in the base-texture picker

### Requirement: A project's stages chain, each building on the previous stage's own baked output
When a target texture is chosen (or explicitly recaptured), the system SHALL capture a snapshot of that texture as currently resolved and generate an implicit "Stage 0" baseline option directly from it, with no overlay applied. Every stage after Stage 0 SHALL composite its own overlay on top of the immediately preceding stage's own baked output — never the pristine snapshot directly (except Stage 0 itself), and never any stage other than its immediate predecessor. Recapturing the snapshot SHALL invalidate Stage 0 and, transitively, every later stage, since each stage's own base has changed.

#### Scenario: A stage builds visibly on the previous stage's look
- **WHEN** a project has Stage 0 (baseline), Stage 1, and Stage 2, each with a different overlay image
- **THEN** Stage 1's baked output shows the baseline plus Stage 1's overlay, and Stage 2's baked output shows Stage 1's own baked result plus Stage 2's overlay on top

#### Scenario: Recapturing the snapshot invalidates the whole chain
- **WHEN** the user recaptures a project's pristine snapshot
- **THEN** Stage 0's baked output is no longer up to date, and every stage after it is transitively considered stale until re-baked in order

#### Scenario: Stage 0 is not user-removable
- **WHEN** the user views a project's stage list
- **THEN** Stage 0 is shown as the project's baseline and cannot be deleted like a user-added stage, since removing it would break every later stage's chain

### Requirement: Applying a project writes a real, permanent Penumbra mod
Applying a project SHALL write a mod to Penumbra's own mod directory containing one single-select option group with one option per configured stage, each option redirecting the fixed Bibo body base texture path (`chara/bibo_mid_base.tex`) to that stage's baked texture file, and SHALL register it with Penumbra so it appears and functions as a normal mod. This capability targets Bibo-body overlays specifically; there is no per-project configurable redirect target.

#### Scenario: Applied project appears as a real mod
- **WHEN** a project is applied
- **THEN** Penumbra shows it as a mod with one option group and one option per stage, toggleable like any other mod

#### Scenario: Only one stage's option is active at a time
- **WHEN** the generated mod's option group is single-select
- **THEN** selecting one stage's option is mutually exclusive with every other stage's option in that group

### Requirement: Re-applying a project updates its mod in place
A project SHALL have a stable generated-mod identity. Applying it again after changes (a stage's image, an added or removed stage) SHALL update that same mod's files and register the update with Penumbra, rather than creating an additional, separate mod.

#### Scenario: Editing a stage and re-applying updates the existing mod
- **WHEN** the user changes a stage's overlay image and applies the project again
- **THEN** the same mod is updated in place, and Penumbra reflects the change after being told to reload it

#### Scenario: Applying twice does not duplicate the mod
- **WHEN** a project is applied more than once without being deleted
- **THEN** Penumbra's mod list still shows only one mod for that project

### Requirement: A project can recover from its generated mod being deleted in Penumbra
A project SHALL offer an explicit "Recreate Mod" action that re-writes the project's mod folder from scratch and re-registers it with Penumbra, regardless of whether the project had already been applied before. This SHALL work even if Penumbra no longer has any record of the mod (e.g. because the user deleted it directly from Penumbra's own UI).

#### Scenario: Recreating a deleted mod
- **WHEN** the user has deleted a project's generated mod directly in Penumbra, then uses "Recreate Mod" on that project
- **THEN** the mod folder is rewritten and the mod reappears in Penumbra as if freshly applied

### Requirement: A project has a configurable priority against other mods
A project SHALL have a per-project priority value that the system applies to the project's generated mod in the local player's active collection whenever the project is applied or its mod is recreated. This priority governs Penumbra's own conflict resolution when more than one enabled mod redirects the same file, and is separate from anything stored inside the generated mod's own `meta.json`.

#### Scenario: Priority is applied when the project is applied
- **WHEN** a project with a configured priority is applied
- **THEN** the generated mod's priority in the local player's active collection is set to that value

#### Scenario: Priority is reapplied when the mod is recreated
- **WHEN** the user uses "Recreate Mod" on a project with a configured priority
- **THEN** the recreated mod's priority in the local player's active collection is set to that same value

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

### Requirement: Generated mods integrate with the existing staged-Penumbra-mod reaction unchanged
A generated mod SHALL be usable by a trigger's existing staged-Penumbra-mod reaction exactly like any other Penumbra mod — picked by mod, option group, and option in that reaction's existing configuration. This capability SHALL introduce no new trigger-firing behavior, no new IPC calls at fire time, and no new failure mode to that already-existing reaction.

#### Scenario: A generated mod's stages are picked in a trigger like any other mod
- **WHEN** the user configures a trigger's staged-Penumbra-mod reaction
- **THEN** a generated mod's option group and options are selectable exactly like a manually authored mod's, with no different behavior

### Requirement: Fields offer discoverable hover guidance
Fields in the Overlay Mod Builder tab whose purpose or behavior is not obvious from their label alone SHALL show a visible help-marker affordance next to the field, and hovering that affordance SHALL show guidance explaining it. No such field may rely on guidance that is reachable only by incidentally hovering the field's own input widget.

#### Scenario: A non-obvious field shows a visible help-marker affordance
- **WHEN** the Overlay Mod Builder tab renders a field whose purpose or behavior is not obvious from its label alone
- **THEN** a visible help-marker affordance is shown next to that field's label

#### Scenario: Hovering the help-marker shows guidance
- **WHEN** the user hovers a field's help-marker affordance
- **THEN** guidance explaining that field is shown

#### Scenario: Previously unguided fields offer guidance
- **WHEN** the user hovers the help-marker for "Display name", "Target", a stage's "Name" field, "Add Project", or "Add Stage"
- **THEN** guidance explaining that field is shown

#### Scenario: Existing action guidance remains reachable via the same affordance
- **WHEN** the user hovers the help-marker for the "Stage 0 — Baseline" label, "Capture Snapshot"/"Recapture Snapshot", "Apply", "Rebake All", "Recreate Mod", "Create Trigger", "Priority", or "Penumbra folder"
- **THEN** guidance explaining that field or action is shown
