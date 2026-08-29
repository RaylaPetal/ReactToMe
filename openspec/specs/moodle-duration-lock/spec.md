# moodle-duration-lock Specification

## Purpose

Lets a reaction trigger's effect-removal timer be marked as never auto-reverting, for pairing with a Moodle the user has manually configured to never expire, since ReactToMe cannot read a Moodle's configured duration or expiration state from Moodles itself.

## Requirements

### Requirement: No-expiration option gated to triggers with a Moodle selected
A reaction trigger SHALL offer a "No expiration" option only when the trigger has a Moodle selected.

#### Scenario: Option available with a Moodle selected
- **WHEN** a trigger has a Moodle assigned
- **THEN** the user can enable "No expiration" for that trigger

#### Scenario: Option unavailable without a Moodle selected
- **WHEN** a trigger has no Moodle assigned
- **THEN** the "No expiration" option is not offered, and the trigger uses its manually-set duration

### Requirement: No-expiration suppresses auto-revert
When "No expiration" is enabled for a trigger, the system SHALL NOT automatically revert the effects applied by that trigger; they remain until manually cleared.

#### Scenario: No-expiration trigger fires
- **WHEN** a trigger with "No expiration" enabled fires
- **THEN** its applied effects are not automatically reverted, and remain active until manually cleared

### Requirement: Manual duration governs revert when not marked no-expiration
When "No expiration" is not enabled, the system SHALL continue to auto-revert the trigger's effects after its manually-set `Duration` elapses, unchanged from existing behavior.

#### Scenario: Trigger without no-expiration fires
- **WHEN** a trigger without "No expiration" enabled fires
- **THEN** its applied effects are automatically reverted after the trigger's manually-set duration elapses
