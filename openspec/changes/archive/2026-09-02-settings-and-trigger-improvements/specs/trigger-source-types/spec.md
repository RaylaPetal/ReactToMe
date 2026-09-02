## MODIFIED Requirements

### Requirement: Chat-phrase source watches a configurable set of channels
A chat-phrase trigger SHALL only match messages from channel types the user has configured as watched (see the plugin-wide watched-channels setting), defaulting to Say, Yell, Shout, Tell, Party, Alliance, and Free Company. Messages from channel types outside the currently-watched set SHALL NOT be matched.

#### Scenario: Watched channel matches
- **WHEN** a matching phrase appears in a channel type that is currently configured as watched
- **THEN** the trigger fires

#### Scenario: Unwatched channel does not match
- **WHEN** a matching phrase appears in a channel type that is not currently configured as watched
- **THEN** the trigger does not fire

## ADDED Requirements

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
