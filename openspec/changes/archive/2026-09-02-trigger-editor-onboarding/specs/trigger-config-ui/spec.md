## MODIFIED Requirements

### Requirement: Fields offer hover guidance
Fields in the detail pane whose purpose or behavior is not obvious from their label alone SHALL show a visible help-marker affordance next to the field, and hovering that affordance SHALL show guidance explaining it. No such field may rely on guidance that is reachable only by incidentally hovering the field's own input widget.

#### Scenario: Hovering a non-obvious field shows guidance
- **WHEN** the user hovers a field that isn't self-explanatory from its label alone
- **THEN** guidance explaining that field is shown

#### Scenario: A non-obvious field shows a visible help-marker affordance
- **WHEN** the detail pane renders a field whose purpose or behavior is not obvious from its label alone
- **THEN** a visible help-marker affordance is shown next to that field's label

#### Scenario: Hovering the help-marker shows guidance
- **WHEN** the user hovers a field's help-marker affordance
- **THEN** guidance explaining that field is shown

#### Scenario: Repeat-refresh and stacking fields offer guidance
- **WHEN** the user hovers the help-marker for "Refresh timer on repeat" or for "Stack multiple"
- **THEN** guidance explaining what that setting controls is shown

#### Scenario: Duration guidance is reachable from its label
- **WHEN** the user hovers the "Duration" label
- **THEN** guidance explaining what Duration controls is shown, regardless of which hour/minute/second sub-field is currently focused

#### Scenario: Penumbra reaction sub-fields offer guidance
- **WHEN** the user hovers the help-marker for the Penumbra reaction "Mode" combo, a stage's "Fire count" field, or a stage's "Mod", "Option group", or "Option" picker
- **THEN** guidance explaining that field is shown

#### Scenario: Revert-to design offers guidance
- **WHEN** the user hovers the help-marker for the "Revert-to design" field
- **THEN** guidance explaining that field is shown
