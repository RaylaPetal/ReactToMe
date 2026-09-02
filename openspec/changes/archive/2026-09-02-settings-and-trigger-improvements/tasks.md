## 1. Configuration additions

- [x] 1.1 Add `ReactionsEnabled` (bool, default `true`) to `Configuration`.
- [x] 1.2 Add `WatchedChatChannels` (a serializable collection of `XivChatType`, default matching today's fixed list: Say, Yell, Shout, Tell, Party, Alliance, Free Company) to `Configuration`.
- [x] 1.3 Add `DefaultTriggerDuration` (`TimeSpan`, default `TimeSpan.FromMinutes(5)`) and `DefaultChatCooldownSeconds` (int, default `10`) to `Configuration`.
- [x] 1.4 Build and confirm 0 warnings/0 errors.

## 2. Character-name filtering

- [x] 2.1 Add `CharacterNameFilter` (string, default empty) to `ReactionTrigger`.
- [x] 2.2 Add `SourceName` (string?) to `EmotePerformedEventArgs` and `JobSkillCastEventArgs`; resolve it in `EmotePoller`/`JobSkillPoller` via their existing `IObjectTable` the same way the source/target game-object-id booleans are already derived.
- [x] 2.3 Add `SenderName` (string) to `ChatMessageReceivedEventArgs`, exposing the name `ChatMessageListener` already resolves internally to compute `SenderIsLocalPlayer`.
- [x] 2.4 Update `TriggerMatcher.FindEmoteMatch`/`FindJobSkillMatch`/`FindChatPhraseMatch` to accept the resolved source/sender name and check it against `t.CharacterNameFilter` (case-insensitive) whenever the filter is non-empty; empty filter matches unconditionally (unchanged behavior). Update `Plugin.cs`'s three `On*` handlers to pass the newly-available name through.
- [x] 2.5 Build and confirm 0 warnings/0 errors.

## 3. Configurable watched chat channels

- [x] 3.1 Change `ChatMessageListener` to watch `Configuration.WatchedChatChannels` instead of its own hardcoded `WatchedChannels` array — read live (not cached at construction) so a Settings change takes effect without a plugin reload.
- [x] 3.2 Build and confirm 0 warnings/0 errors.

## 4. Master switch

- [x] 4.1 In `Plugin.cs`, add an early return (`if (!Configuration.ReactionsEnabled) return;`) at the top of `OnEmotePerformed`, `OnJobSkillCast`, and `OnChatMessageReceived`, before any matching work.
- [x] 4.2 Build and confirm 0 warnings/0 errors.

## 5. New-trigger defaults

- [x] 5.1 Update "Add Trigger" (`ConfigWindow.cs`) to construct a new `ReactionTrigger` with `Duration = configuration.DefaultTriggerDuration` and `ChatCooldownSeconds = configuration.DefaultChatCooldownSeconds` instead of relying on the class's own property-initializer defaults.
- [x] 5.2 Build and confirm 0 warnings/0 errors.

## 6. Settings tab

- [x] 6.1 Add a "Reactions enabled" checkbox to `DrawSettingsTab`, bound to `configuration.ReactionsEnabled`.
- [x] 6.2 Add a "Watched Chat Channels" section to `DrawSettingsTab`: one checkbox per channel type (the existing seven plus Linkshell, Cross-world Linkshell, Novice Network), toggling membership in `configuration.WatchedChatChannels`. Implemented as 10 checkboxes (not 7+16=23): the individual Linkshells (Ls1-8) and Cross-world Linkshells (CrossLinkShell1-8) are each grouped under one "Linkshells"/"Cross-world Linkshells" checkbox that toggles all 8 of that family as a unit, since a user wants "watch my linkshells" as one concept, not to pick individually among 8 numbered channels — laid out 3 per row to stay compact.
- [x] 6.3 Add "New Trigger Defaults" inputs to `DrawSettingsTab`: default duration (minutes) and default chat cooldown (seconds), bound to `configuration.DefaultTriggerDuration`/`DefaultChatCooldownSeconds`.
- [x] 6.4 Build and confirm 0 warnings/0 errors.

## 7. Trigger removal confirmation

- [x] 7.1 Add a `pendingRemoveTriggerId` field to `ConfigWindow` (mirroring `pendingRemoveOverlayProjectId`) and change "Remove Trigger" into the same two-click confirm/cancel flow already used for Overlay Mod Builder project removal.
- [x] 7.2 Build and confirm 0 warnings/0 errors.

## 8. Character-name filter field + Test Fire

- [x] 8.1 Add a "Character name filter" text field to the trigger detail pane's source section, bound to `trigger.CharacterNameFilter`, with a tooltip explaining it has no effect when the trigger's scope/source-of-message is already self-only.
- [x] 8.2 Expose `Plugin.FireReactions` for `ConfigWindow` to call directly (`internal`, or a thin `TestFireTrigger(trigger)` wrapper) and add a "Test Fire" button to the trigger detail pane that calls it with the currently-selected trigger.
- [x] 8.3 Build and confirm 0 warnings/0 errors.

## 9. End-to-end verification

- [x] 9.1 Manually verify the full scenario set in the delta specs: turning the master switch off stops a trigger from firing and back on restores it without altering per-trigger enabled state; opting into a previously-unwatched chat channel makes a phrase there start matching; a new trigger seeds from the configured defaults while existing triggers are unaffected by a later default change; a character-name filter restricts an "anyone"/"others"-scoped trigger to one character and has no effect on a self-only trigger; removing a trigger requires confirming; Test Fire applies a trigger's reactions immediately and is subject to the same chat cooldown a real repeat fire would be. Confirmed by the user.
