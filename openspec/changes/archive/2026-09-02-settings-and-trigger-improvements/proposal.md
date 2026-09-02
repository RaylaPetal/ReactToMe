## Why

An explore session surfaced that the Settings tab has never grown past its original two checkboxes, while several values that would obviously belong there — the watched chat channels, a new trigger's seed duration/cooldown, and a plugin-wide "pause everything" switch — are currently either hardcoded or absent entirely. The same session also flagged two trigger-editing gaps: "Remove Trigger" deletes instantly with no confirmation (unlike the two-click confirm the Overlay Mod Builder's project removal already has for an equally irreversible action), and there's no way to validate a trigger's reactions without waiting for the real emote/chat phrase/job skill to happen, or to restrict a trigger to a specific character rather than "anyone nearby."

This proposes the subset of that exploration judged most valuable to build now, deferring lower-value/larger items (config export/import, trigger duplication, trigger grouping/tags, Overlay Mod Builder project search) as explicit non-goals for a later pass.

## What Changes

- Add a plugin-wide "Reactions enabled" master switch (Settings tab) that pauses all trigger matching without touching any individual trigger's own `IsEnabled` state — a fast way to go quiet for a call/stream without losing per-trigger configuration.
- Make the chat-phrase trigger source's watched channel set configurable (Settings tab), replacing the current fixed list (Say/Yell/Shout/Tell/Party/Alliance/Free Company) with per-channel checkboxes that also let the user opt into Linkshell, Cross-world Linkshell, and Novice Network. Defaults to today's fixed set, so existing behavior is unchanged until the user opts into more.
- Add configurable defaults (Settings tab) for a newly-added trigger's effect duration and chat cooldown, replacing the currently-hardcoded model defaults (5 minutes, 10 seconds) as the seed values "Add Trigger" starts a new trigger with. Existing triggers are unaffected.
- Add a "Remove Trigger" confirmation step (two-click, matching the pattern already used for Overlay Mod Builder project removal) so a trigger can no longer be deleted by a single accidental click.
- Add an optional per-trigger character-name filter, usable alongside a trigger's existing scope/source-of-message setting (emote/job-skill `TriggerScope`, chat-phrase `ChatTriggerSource`) to further restrict matching to one specific character's name — meaningful whenever the existing setting allows anyone other than just the local player; empty (the default) matches exactly as today.
- Add a "Test Fire" button to a trigger's detail pane that fires its configured reactions immediately, exactly as a real matched event would, so the wiring can be validated without waiting for the actual emote/chat phrase/job skill.

## Capabilities

### New Capabilities

- `plugin-settings`: plugin-wide configuration reachable from the Settings tab — a master pause switch, the configurable chat-channel watch list, and new-trigger seed defaults.

### Modified Capabilities

- `trigger-source-types`: the chat-phrase source's watched channel set becomes configurable instead of fixed; trigger matching gains an optional character-name filter applicable across all three source types.
- `trigger-config-ui`: "Remove Trigger" requires a confirmation step; the detail pane gains a "Test Fire" action.

## Impact

- `ReactToMe/Configuration.cs`: add `ReactionsEnabled` (bool, default true), `WatchedChatChannels` (configurable channel set, defaulting to today's fixed list), `DefaultTriggerDuration`/`DefaultChatCooldownSeconds` (seed values for new triggers).
- `ReactToMe/Triggers/ReactionTrigger.cs`: add an optional `CharacterNameFilter` (string, default empty) field.
- `ReactToMe/Triggers/TriggerMatcher.cs`: `MatchesScope`/`FindChatPhraseMatch` take the source's resolved character name and check it against a trigger's `CharacterNameFilter` when set.
- `ReactToMe/Emotes/EmotePoller.cs`, `ReactToMe/JobSkills/JobSkillPoller.cs`: resolve and expose the source character's name (already have `IObjectTable` access) alongside the existing game-object-id fields.
- `ReactToMe/ChatDetection/ChatMessageListener.cs`: expose the sender's name in `ChatMessageReceivedEventArgs`; watch the configured channel set instead of the current hardcoded `WatchedChannels` array.
- `ReactToMe/Plugin.cs`: check `Configuration.ReactionsEnabled` before matching in each of the three `On*` handlers; a new `TestFireTrigger(trigger)` entry point reusing the existing `FireReactions` path.
- `ReactToMe/Windows/ConfigWindow.cs`: Settings tab gains the master switch, chat-channel checkboxes, and new-trigger-defaults inputs; Triggers tab's "Remove Trigger" gains a two-click confirm (mirroring the existing Overlay Mod Builder project-removal pattern); trigger detail pane gains a character-name filter field and a "Test Fire" button.
- No changes to the Overlay Mod Builder tab or capability, and no changes to how an already-matched trigger applies its reactions (Glamourer/Moodle/Penumbra/chat/gesture) — this only changes whether/which triggers get to that point, plus the two isolated UI additions (confirm-delete, test-fire).
