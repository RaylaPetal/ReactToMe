## Purpose

Lets a reaction trigger choose what Glamourer state its timer reverts to when it expires — Glamourer's own automation (today's only behavior) or a specific base design the user picks — instead of always handing control back to automation.

## ADDED Requirements

### Requirement: Trigger has a revert-target choice
A reaction trigger SHALL offer a choice of revert target for when its timer expires: "Revert to automation" or "Apply a specific design." The default SHALL be "Revert to automation," matching existing behavior for triggers that don't change it.

#### Scenario: Default revert target is automation
- **WHEN** a trigger has not had its revert target changed
- **THEN** its timer expiry reverts to Glamourer automation, as before this option existed

#### Scenario: User selects a specific base design as the revert target
- **WHEN** a trigger's revert target is set to "Apply a specific design" with a chosen design
- **THEN** the trigger's timer expiry applies that design instead of reverting to automation

### Requirement: Timer expiry uses the trigger's configured revert target
When a trigger's timer expires, the system SHALL use the revert target configured on that trigger to determine the resulting Glamourer state: automation, or the trigger's chosen base design.

#### Scenario: Trigger configured to revert to automation expires
- **WHEN** a trigger whose revert target is "Revert to automation" expires
- **THEN** the system reverts the local player to Glamourer automation

#### Scenario: Trigger configured to revert to a base design expires
- **WHEN** a trigger whose revert target is "Apply a specific design" (with design D) expires
- **THEN** the system applies design D to the local player

### Requirement: Manual force-revert always resets to automation
The manual "force revert all" action SHALL always revert the local player to Glamourer automation, regardless of any active trigger's configured revert target, so it remains a full escape hatch.

#### Scenario: Force-revert used while an effect targets a specific base design
- **WHEN** the user triggers a manual force-revert while an active effect's trigger is configured to revert to a specific base design
- **THEN** the local player is reverted to Glamourer automation, not the configured base design
