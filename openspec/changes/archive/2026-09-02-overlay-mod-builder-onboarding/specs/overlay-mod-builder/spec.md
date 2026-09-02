## ADDED Requirements

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
