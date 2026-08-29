## Purpose

Lets a reaction trigger make the local player perform an emote as one of its reactions, alongside its existing Glamourer, Moodle, chat message, and Penumbra stage reactions.

## ADDED Requirements

### Requirement: Trigger can configure a gesture reaction
A reaction trigger SHALL offer an optional gesture reaction: an emote for the local player to perform when the trigger fires. This reaction is independent of the trigger's other reactions — any combination may be configured together.

#### Scenario: No gesture configured
- **WHEN** a trigger has no gesture configured
- **THEN** firing it does not make the local player perform any emote

#### Scenario: Gesture configured
- **WHEN** a trigger has a gesture configured and fires
- **THEN** the local player performs that emote

### Requirement: Gesture reaction is fire-and-forget
A gesture reaction SHALL have no duration or revert behavior — it is performed once per fire and is not tracked as an ongoing effect.

#### Scenario: Gesture does not participate in the revert timer
- **WHEN** a trigger with both a Glamourer design and a gesture reaction fires, and its timer later expires
- **THEN** the Glamourer design reverts per its configured behavior, and the gesture (already performed once at fire time) is unaffected — there is nothing to revert

### Requirement: Gesture reaction shares the chat-send cooldown
A trigger's gesture reaction and its chat-message reaction SHALL share the same per-trigger send cooldown, since both are sent through the same chat-submission path.

#### Scenario: Chat message suppressed by cooldown also suppresses the gesture
- **WHEN** a trigger's chat-send cooldown has not yet elapsed since its last chat-message or gesture send, and it fires again with both configured
- **THEN** neither the chat message nor the gesture is sent for this fire

### Requirement: Only emotes with a resolvable command are offered
A trigger's gesture picker SHALL only offer emotes that resolve to a usable command. Emotes without one SHALL NOT be selectable as a gesture reaction.

#### Scenario: Emote without a command is not offered
- **WHEN** an emote has no resolvable command
- **THEN** it does not appear in the gesture picker
