## ADDED Requirements

### Requirement: Removing a trigger requires confirmation
Removing a trigger SHALL require an explicit confirmation step, separate from the initial removal action, since deleting a trigger cannot be undone.

#### Scenario: Removal without confirmation does not delete the trigger
- **WHEN** the user starts removing a trigger but does not complete the confirmation step
- **THEN** the trigger is not deleted

#### Scenario: Confirming removal deletes the trigger
- **WHEN** the user removes a trigger and completes the confirmation step
- **THEN** the trigger is deleted

### Requirement: A trigger's reactions can be tested on demand
The detail pane SHALL offer a "Test Fire" action that fires a trigger's currently-configured reactions immediately, using the same mechanism a real matched event would, so its wiring can be validated without waiting for its actual source condition to occur.

#### Scenario: Test Fire applies the trigger's reactions
- **WHEN** the user selects "Test Fire" on a trigger with reactions configured
- **THEN** those reactions are applied exactly as they would be from a real matching event

#### Scenario: Test Fire respects the same cooldown as a real fire
- **WHEN** the user selects "Test Fire" on a trigger with a chat message and cooldown configured, then immediately does so again before the cooldown elapses
- **THEN** the second Test Fire's chat message is withheld by the same cooldown a real repeated fire would be subject to
