## MODIFIED Requirements

### Requirement: Base texture is picked from a combined list of live and declared paths whose immediate parent directory is "chara"
A project's base texture — what its pristine snapshot reads pixel data from — SHALL be chosen from a searchable list combining two sources, both kept only where the game path's immediate parent directory is named `chara` (e.g. `chara/bibo_mid_base.tex`, or an absolute path like `Z:\...\Skin Overlay Kaede\chara\kaede overlay.tex` — what precedes `chara` doesn't matter): every texture Penumbra currently reports as actively resolving for the local player, and every installed mod's own declared file redirect read directly from that mod's files on disk (regardless of whether the mod is currently enabled). This pick is independent of what a generated mod actually redirects (see the priority/redirect requirements above).

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

## ADDED Requirements

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
