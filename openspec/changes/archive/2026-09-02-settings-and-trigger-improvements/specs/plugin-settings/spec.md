## Purpose

Plugin-wide configuration reachable from the Settings tab — behavior that applies across every trigger at once, rather than being configured per trigger.

## ADDED Requirements

### Requirement: A master switch pauses all trigger reactions without changing per-trigger state
The Settings tab SHALL offer a single switch that, when off, prevents every trigger from matching or firing, regardless of each trigger's own enabled state. Turning the switch back on SHALL restore matching exactly as each trigger was individually configured, with no trigger's own enabled state changed by the switch itself.

#### Scenario: Switching off stops all triggers from firing
- **WHEN** the master switch is off
- **THEN** no trigger fires, even one whose own enabled state is on and whose source condition occurs

#### Scenario: Switching back on restores prior per-trigger state
- **WHEN** the master switch is turned back on
- **THEN** every trigger's own enabled state is exactly what it was before the switch was turned off

#### Scenario: The switch does not affect already-active effects
- **WHEN** the master switch is turned off while an effect from an earlier fire is still active
- **THEN** that already-active effect is unaffected — it continues and reverts on its own schedule as configured

### Requirement: Watched chat channels are configurable
The Settings tab SHALL let the user choose which chat channel types are watched for chat-phrase trigger matching, defaulting to the previously-fixed set (Say, Yell, Shout, Tell, Party, Alliance, Free Company) so existing behavior is unchanged until the user opts into more. Additional channel types (at minimum Linkshell, Cross-world Linkshell, and Novice Network) SHALL be available to opt into.

#### Scenario: Default channel set matches prior fixed behavior
- **WHEN** the user has never changed the watched-channel setting
- **THEN** chat-phrase triggers match exactly the channel types they always did

#### Scenario: Opting into an additional channel
- **WHEN** the user enables watching a channel type that was not previously watched (e.g. Linkshell)
- **THEN** a matching phrase seen in that channel type now fires a chat-phrase trigger

#### Scenario: Opting out of a previously-watched channel
- **WHEN** the user disables a previously-watched channel type
- **THEN** a matching phrase seen in that channel type no longer fires a chat-phrase trigger

### Requirement: New triggers seed from configurable defaults
The Settings tab SHALL let the user configure the effect duration and chat cooldown that a newly-created trigger starts with, replacing the previously-hardcoded seed values. Changing these defaults SHALL NOT alter any already-existing trigger's own configured values.

#### Scenario: A new trigger uses the configured defaults
- **WHEN** the user creates a new trigger after changing the default duration and chat cooldown
- **THEN** the new trigger's duration and chat cooldown start at those configured values

#### Scenario: Existing triggers are unaffected by a default change
- **WHEN** the user changes the default duration or chat cooldown
- **THEN** every already-existing trigger keeps its own previously-configured values unchanged
