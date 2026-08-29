## MODIFIED Requirements

### Requirement: The active stage is the highest threshold reached
At any fire count, the system SHALL treat the active stage as the configured entry with the greatest threshold less than or equal to the current fire count. If the fire count has not yet reached any configured threshold, no stage is active. Whenever the resolved stage differs from what's currently applied, the system SHALL enable that stage's mod (if not already enabled) and set its option group to that stage's option, via Penumbra's IPC, using a temporary, player-scoped setting rather than a change to the mod's permanent configuration. A fire that does not change the resolved stage SHALL NOT re-issue that call.

#### Scenario: Fire count below the lowest threshold applies nothing yet
- **WHEN** a trigger's stages are configured with thresholds 5, 10, and 15, and its fire count is 3
- **THEN** no stage has been applied for this trigger yet

#### Scenario: Fire count reaching a threshold applies its stage
- **WHEN** a trigger's stages are configured with thresholds 1, 5, and 10, and its fire count reaches 5
- **THEN** the mod/option group/option configured at threshold 5 is applied

#### Scenario: Fire count between thresholds holds the last-reached stage
- **WHEN** a trigger's stages are configured with thresholds 1, 5, and 10, and its fire count is 7
- **THEN** the stage configured at threshold 5 remains applied, since fire count 7 has not reached threshold 10

## ADDED Requirements

### Requirement: Penumbra changes are temporary, never permanent
The system SHALL apply and remove staged Penumbra mod settings using Penumbra's temporary-settings mechanism, scoped to the local player, and SHALL NOT write to a mod's permanent configuration. Removing a trigger's temporary override SHALL restore the mod to whatever its permanent configuration already was, not to a hard-coded disabled state.

#### Scenario: A stage never persists after being cleared
- **WHEN** a trigger's staged mod is enabled and later disabled (by timer expiry or manual force-revert)
- **THEN** the mod's permanent configuration is unchanged from before the trigger ever fired

#### Scenario: A mod the user permanently enabled stays enabled after ReactToMe clears its override
- **WHEN** a trigger stages a mod that the user has separately, permanently enabled in Penumbra, and ReactToMe's temporary override is later cleared
- **THEN** the mod remains enabled, per the user's own permanent configuration — clearing ReactToMe's override does not disable it

### Requirement: A mod's configured priority is preserved
When applying a staged mod, the system SHALL use that mod's own currently configured priority rather than a fixed value, so a temporary override never changes how the mod resolves against others while it's active.

#### Scenario: A non-default priority is preserved
- **WHEN** a mod has a non-default priority configured (permanently, outside ReactToMe) and a trigger stages it
- **THEN** the mod's priority while the temporary override is active is unchanged from its configured value
