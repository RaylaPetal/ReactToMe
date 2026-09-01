## Purpose

Lets the user author a permanent, multi-stage Penumbra mod from imported overlay images, baked against a pristine snapshot of a chosen character texture, so the existing staged-Penumbra-mod trigger reaction can escalate through them exactly like any other pre-built mod.

## ADDED Requirements

### Requirement: A dedicated tab hosts mod-authoring, separate from trigger configuration
The config screen SHALL offer a top-level tab, alongside Active Effects / Triggers / Settings, dedicated to authoring overlay-mod-builder projects. This workflow is independent of configuring any trigger — a project's stages are plain Penumbra mod options once applied, not something a trigger references directly by project.

#### Scenario: The tab is reachable without opening a trigger
- **WHEN** the user wants to create or edit an overlay-mod-builder project
- **THEN** they switch to its own tab, without needing to open any trigger's configuration

### Requirement: Base texture is picked from a searchable, unfiltered live list
A project's base texture — what its pristine snapshot reads pixel data from — SHALL be chosen from a searchable list of every texture currently resolving for the local player, identified by its exact game path, not grouped or filtered by any assumed naming convention. This pick is independent of what a generated mod actually redirects (see below): some live-resolving textures report their own resolved file as their "game path" with no separate virtual identity (e.g. a mod that rewrites a material to reference its own file directly), so the picked path is not assumed to be a redirectable virtual game path.

#### Scenario: Picker reflects the player's actual current textures
- **WHEN** the target-texture picker is opened
- **THEN** it lists every texture currently resolving for the local player, searchable by path

#### Scenario: A picked texture with no separate resolved file still works
- **WHEN** a live-resolving texture reports no separate actual file (its game path is already the terminal, resolved file)
- **THEN** the picker still resolves it to a readable file, rather than leaving the project unable to read a base texture

### Requirement: A project's stages composite against one pristine snapshot, captured once
When a target texture is chosen (or explicitly recaptured), the system SHALL capture a snapshot of that texture as currently resolved and reuse that same snapshot as the base for every stage's composite. Baking a stage SHALL NOT read the live texture again, and SHALL NOT composite on top of another stage's output — every stage is independently "pristine snapshot + that stage's own overlay."

#### Scenario: Two stages composite independently
- **WHEN** a project has two stages with different overlay images
- **THEN** both are composited from the same pristine snapshot, and neither stage's baked output includes the other's overlay

#### Scenario: Recapturing the snapshot is a deliberate action
- **WHEN** the user wants a project's stages to reflect a changed skin/body mod
- **THEN** they explicitly recapture the snapshot, which does not happen automatically at any other time

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

### Requirement: Generated mods integrate with the existing staged-Penumbra-mod reaction unchanged
A generated mod SHALL be usable by a trigger's existing staged-Penumbra-mod reaction exactly like any other Penumbra mod — picked by mod, option group, and option in that reaction's existing configuration. This capability SHALL introduce no new trigger-firing behavior, no new IPC calls at fire time, and no new failure mode to that already-existing reaction.

#### Scenario: A generated mod's stages are picked in a trigger like any other mod
- **WHEN** the user configures a trigger's staged-Penumbra-mod reaction
- **THEN** a generated mod's option group and options are selectable exactly like a manually authored mod's, with no different behavior
