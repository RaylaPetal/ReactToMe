# trigger-source-types Specification

## Purpose

Lets a reaction trigger fire from a chat phrase or a job-skill cast, not only an emote, by generalizing "what fires this trigger" into one of several source types with their own matching rules.

## Requirements

### Requirement: A trigger has exactly one source type
A reaction trigger SHALL have exactly one source type: emote, chat phrase, or job skill. Only the fields relevant to the selected source type SHALL be used for matching; fields belonging to other source types SHALL be ignored regardless of their stored value.

#### Scenario: Emote source type matches as before
- **WHEN** a trigger's source type is emote, with an emote and scope configured
- **THEN** it fires exactly as emote-sourced triggers already do, unaffected by this change

#### Scenario: Non-matching source type fields are ignored
- **WHEN** a trigger's source type is chat phrase, and it also has a leftover emote or job-skill configuration from before its source type was changed
- **THEN** only the chat-phrase configuration is used to decide whether it fires

### Requirement: Chat-phrase source matches a free-form phrase
A trigger with a chat-phrase source SHALL fire when an incoming chat message contains its configured phrase as a case-insensitive substring. The phrase is free-form per trigger — there is no fixed required suffix or prefix.

#### Scenario: Phrase found in a chat message
- **WHEN** a trigger's chat phrase is "spank react" and a watched chat message contains that text anywhere, case-insensitively
- **THEN** the trigger fires

#### Scenario: Phrase not present
- **WHEN** a watched chat message does not contain the trigger's configured phrase
- **THEN** the trigger does not fire

### Requirement: Chat-phrase source chooses who it fires for
A chat-phrase trigger SHALL choose between two sources: only messages the local player types themselves, or messages from anyone nearby (including the local player).

#### Scenario: Self-typed only
- **WHEN** a chat-phrase trigger is set to fire only for the local player's own messages, and another player's message contains the matching phrase
- **THEN** the trigger does not fire

#### Scenario: Anyone nearby
- **WHEN** a chat-phrase trigger is set to fire for anyone nearby, and another player's watched-channel message contains the matching phrase
- **THEN** the trigger fires

### Requirement: Chat-phrase source watches a configurable set of channels
A chat-phrase trigger SHALL only match messages from channel types the user has configured as watched (see the plugin-wide watched-channels setting), defaulting to Say, Yell, Shout, Tell, Party, Alliance, and Free Company. Messages from channel types outside the currently-watched set SHALL NOT be matched.

#### Scenario: Watched channel matches
- **WHEN** a matching phrase appears in a channel type that is currently configured as watched
- **THEN** the trigger fires

#### Scenario: Unwatched channel does not match
- **WHEN** a matching phrase appears in a channel type that is not currently configured as watched
- **THEN** the trigger does not fire

### Requirement: A trigger can be further restricted to one specific character
Any trigger — regardless of source type — SHALL support an optional character-name filter that, when set, restricts matching to that one character by name, in addition to whatever its existing scope/source-of-message setting already allows. The filter has no effect when a trigger's existing scope/source-of-message setting already restricts it to only the local player (there is no "other character" to filter among in that case). An empty filter (the default) matches exactly as if the filter did not exist.

#### Scenario: Filter narrows an "anyone"/"others"-scoped trigger to one character
- **WHEN** an emote trigger's scope allows others besides the local player, and its character-name filter is set to a specific character's name
- **THEN** the trigger fires only when that specific character performs the matching emote, not when any other character does

#### Scenario: Empty filter matches as before
- **WHEN** a trigger's character-name filter is empty
- **THEN** matching behaves exactly as it did before this filter existed

#### Scenario: Filter has no effect on a self-only trigger
- **WHEN** a trigger's scope/source-of-message setting already restricts it to only the local player, and a character-name filter is also set
- **THEN** the filter has no additional effect, since the trigger already matches only the local player

#### Scenario: Filter applies to chat-phrase triggers too
- **WHEN** a chat-phrase trigger's source-of-message setting is "anyone nearby" and its character-name filter names a specific character
- **THEN** the trigger fires only when that specific character sends the matching phrase, not when anyone else does

### Requirement: Job-skill source matches a cast-time action being cast
A trigger with a job-skill source SHALL fire when its configured action is cast by a character matching its scope, detected while that action has an active cast (a nonzero cast time). Instant actions that never enter a casting state are not detectable by this source type and are out of scope.

#### Scenario: Configured cast-time action is cast
- **WHEN** a trigger's job-skill source names an action with a cast bar, and a character matching the trigger's scope begins casting that action
- **THEN** the trigger fires

#### Scenario: Scope restricts who triggers it
- **WHEN** a job-skill trigger's scope is "others targeting me" and the local player casts the configured action at someone else
- **THEN** the trigger does not fire

#### Scenario: Instant action cannot be configured as a reliable source
- **WHEN** a job-skill trigger names an action that has no cast time
- **THEN** casting that action is not guaranteed to fire the trigger, since instant actions do not produce a detectable cast state
