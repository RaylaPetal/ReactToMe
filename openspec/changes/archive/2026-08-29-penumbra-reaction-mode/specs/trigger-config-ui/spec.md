## ADDED Requirements

### Requirement: Penumbra reaction chooses a mode
A trigger's Penumbra reaction SHALL have exactly one mode: None, Single, or Staged. The Reactions section's Penumbra editor SHALL show only the fields relevant to the selected mode: None shows just the mode choice and no mod/option fields; Single shows a flat mod picker plus whatever option fields that mod's group count calls for (see "Mod option fields adapt to how many option groups a mod has") with no fire-count threshold field and no tab bar; Staged shows the existing tabbed, threshold-driven editor.

#### Scenario: None mode shows no mod fields
- **WHEN** a trigger's Penumbra reaction mode is None
- **THEN** the Reactions section shows only the mode choice, with no mod, option-group, option, or threshold fields

#### Scenario: Single mode shows flat fields with no threshold
- **WHEN** a trigger's Penumbra reaction mode is Single
- **THEN** the Reactions section shows a mod picker and that mod's option fields (per its group count) with no fire-count threshold field and no tab bar

#### Scenario: Staged mode shows the tabbed threshold editor
- **WHEN** a trigger's Penumbra reaction mode is Staged
- **THEN** the Reactions section shows the tabbed stage editor, with each stage's own threshold, mod, and option fields (per its group count), exactly as already specified for staged Penumbra editing

### Requirement: Mod option fields adapt to how many option groups a mod has
Not every Penumbra mod has option groups, and not every mod with options has more than one group. The mod-option fields (used by both Single mode and each Staged-mode stage) SHALL adapt to the selected mod's actual group count: a mod with no option groups SHALL show no option-group or option fields at all; a mod with exactly one option group SHALL show only that group's option picker, without a separate option-group selector; a mod with two or more option groups SHALL show an option-group selector followed by the selected group's option picker.

#### Scenario: A mod with no option groups shows no option fields
- **WHEN** the selected mod has zero option groups
- **THEN** no option-group selector or option picker is shown for it

#### Scenario: A mod with exactly one option group skips the group selector
- **WHEN** the selected mod has exactly one option group
- **THEN** only that group's option picker is shown, with no separate option-group selector

#### Scenario: A mod with multiple option groups shows both selectors
- **WHEN** the selected mod has two or more option groups
- **THEN** an option-group selector is shown, and selecting a group reveals its own option picker

### Requirement: Switching Penumbra reaction mode preserves compatible configuration
When the user switches a trigger's Penumbra reaction mode, the system SHALL carry over configuration that remains meaningful in the new mode and discard what does not.

#### Scenario: Single to Staged keeps the entry and reveals its threshold
- **WHEN** a trigger's mode changes from Single (with a mod/option configured) to Staged
- **THEN** that mod/option becomes the first stage, and its fire-count threshold field becomes visible and editable

#### Scenario: Staged to Single keeps only the first stage
- **WHEN** a trigger's mode changes from Staged (with multiple stages configured) to Single
- **THEN** only the first stage's mod and option are kept, and any other stages are discarded

#### Scenario: Switching to None clears the configured mod reaction
- **WHEN** a trigger's mode changes to None
- **THEN** any previously configured mod, option-group, option, and threshold values for this trigger's Penumbra reaction are cleared

### Requirement: Pre-existing Penumbra stage configuration loads as Staged mode
A trigger whose Penumbra stages were configured before this mode selector existed SHALL be interpreted as Staged mode on load, so its existing behavior and presentation are unchanged after upgrading.

#### Scenario: A trigger with existing stages opens in Staged mode
- **WHEN** a trigger configured before this change has one or more Penumbra stages already set up
- **THEN** it loads with its Penumbra reaction mode as Staged, showing the same tabbed editor and applying the same matching/escalation behavior as before this change
