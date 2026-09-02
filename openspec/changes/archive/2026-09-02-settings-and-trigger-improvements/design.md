## Context

See proposal.md for motivation. Current state this builds on:

- `TriggerMatcher.Find*Match` (`ReactToMe/Triggers/TriggerMatcher.cs`) matches purely on booleans (`sourceIsLocalPlayer`, `targetIsLocalPlayer`, `senderIsLocalPlayer`) — no actual character name ever reaches this layer today, for any of the three source types.
- `EmotePerformedEventArgs`/`JobSkillCastEventArgs` (`ReactToMe/Emotes/EmotePoller.cs`, `ReactToMe/JobSkills/JobSkillPoller.cs`) carry only `SourceGameObjectId`/`TargetGameObjectId` — both pollers already hold an `IObjectTable` reference used to read the native struct data, so resolving a name from an id is a small addition, not a new dependency.
- `ChatMessageReceivedEventArgs` (`ReactToMe/ChatDetection/ChatMessageListener.cs`) computes `SenderIsLocalPlayer` internally by comparing against the local player's name, but never exposes that name itself to callers.
- `ChatMessageListener.WatchedChannels` is a `private static readonly XivChatType[]` — a fixed compile-time list, not read from `Configuration` at all.
- `Plugin.cs`'s three `On*` handlers (`OnEmotePerformed`, `OnJobSkillCast`, `OnChatMessageReceived`) each independently call the matching `TriggerMatcher.Find*Match`, then `FireReactions(trigger)` if non-null. `FireReactions` is private.
- `ReactionTrigger`'s `Duration` (default `TimeSpan.FromMinutes(5)`) and `ChatCooldownSeconds` (default `10`) are hardcoded property initializers — the values a new trigger gets are whatever the class defaults to, with no seam for a user-facing default.
- The Overlay Mod Builder's project-removal two-click confirm (`pendingRemoveOverlayProjectId` field, `ConfigWindow.cs`) is the existing precedent this change's trigger-removal confirmation follows.

## Goals / Non-Goals

**Goals:**
- A user can pause all reactions instantly without losing any trigger's own configuration.
- Chat-phrase matching's watched channels and a new trigger's seed values move from hardcoded to configurable, without changing any existing trigger's or channel's current behavior by default.
- A trigger can be pinned to one specific character wherever "anyone"/"others" was previously the only option.
- A trigger's reactions can be validated on demand, through the exact same code path a real fire uses.
- Deleting a trigger requires the same kind of confirmation an equally-irreversible action (deleting a real Penumbra mod) already requires elsewhere in this plugin.

**Non-Goals:**
- No config export/import, trigger duplication, trigger grouping/tags, or Overlay Mod Builder project-list search — all flagged during exploration as lower-value or larger, deferred to a later change.
- No character-name *picker* (e.g. from a nearby-player list) — the filter is a plain typed name, matched case-insensitively, the same level of polish `ChatPhrase` matching already has.
- No per-channel-type default beyond the fixed set already in place — Linkshell/CWLS/Novice Network are added as available options, not turned on by default.
- No change to how `FireReactions` itself applies reactions once a trigger is chosen — Test Fire and a real fire both end at the exact same call.

## Decisions

- **The master switch is checked once, at the top of each of `Plugin.cs`'s three `On*` handlers**, before any matching work happens at all (`if (!Configuration.ReactionsEnabled) return;`) — cheaper than checking inside `TriggerMatcher` per-trigger, and keeps `TriggerMatcher` a pure function of its inputs with no `Configuration` dependency. Already-active effects are untouched, since this only gates new matches, not `ActiveEffectRegistry`'s own tick/revert logic.
- **Character names are resolved once, at the source, not inside `TriggerMatcher`.** `EmotePoller`/`JobSkillPoller` already hold `IObjectTable` and resolve names the same way `ActiveEffectRegistry`/`Plugin` resolve the local player today (`ObjectTable.SearchById(id)?.Name.TextValue`); the resolved name (nullable — an id that no longer resolves, e.g. the actor left render range between the event and this lookup, yields no filter match) is added to `EmotePerformedEventArgs`/`JobSkillCastEventArgs` as `SourceName`. `ChatMessageListener` already has the sender's name in hand while computing `SenderIsLocalPlayer` and exposes it as `SenderName` on `ChatMessageReceivedEventArgs`. `TriggerMatcher.Find*Match` gains a `string? sourceName` parameter, checked against `t.CharacterNameFilter` (case-insensitive exact match) only when the filter is non-empty.
- **The character-name filter is one shared field (`ReactionTrigger.CharacterNameFilter`), not per-source-type**, since "restrict to one character" means the same thing regardless of whether the trigger's underlying source is an emote, a chat phrase, or a job skill — avoids three near-identical fields for the same concept.
- **Watched chat channels move to `Configuration.WatchedChatChannels`** (a `List<XivChatType>` or equivalent serializable set), defaulted at construction to today's exact fixed list, so a config saved before this change (where the field is absent and deserializes to its default) behaves identically to today. `ChatMessageListener` takes this set (or a way to read it live) instead of its own hardcoded array.
- **New-trigger defaults live on `Configuration`** (`DefaultTriggerDuration`, `DefaultChatCooldownSeconds`), read only at the moment "Add Trigger" constructs a new `ReactionTrigger` — every other read of `Duration`/`ChatCooldownSeconds` continues to come from the trigger's own stored value, exactly as today.
- **Trigger-removal confirmation reuses the Overlay Mod Builder's exact two-click pattern** (a pending-id field checked against the item being drawn, a "Confirm"/"Cancel" pair replacing the single button once pending) rather than inventing a second UI convention for the same kind of decision.
- **"Test Fire" reuses `FireReactions` directly** — `Plugin.FireReactions` becomes `internal` (or gains a thin public wrapper) so `ConfigWindow` can call it with a specific trigger, with no new reaction-application code path to keep in sync with the real one. Because it goes through the same method, it naturally inherits the existing chat/gesture cooldown keyed by trigger id — a Test Fire counts as a fire for cooldown purposes, matching the spec's explicit scenario.

## Risks / Trade-offs

- [Risk] Resolving a character's name from a `GameObjectId` at the moment of the event could occasionally return null if the actor has already left render range — the character-name filter then simply never matches for that occurrence. -> Acceptable: the same actor being out of range makes the underlying emote/job-skill itself unobservable by this plugin's existing polling approach anyway, so this isn't a new failure mode, just the existing one surfacing here too.
- [Risk] Making `Plugin.FireReactions` more accessible (from private) slightly widens `Plugin`'s internal surface. -> Mitigation: expose the minimum needed (internal, or a single-purpose `TestFireTrigger` wrapper) rather than making it broadly public.
- [Risk] A config saved before `WatchedChatChannels` existed must deserialize to exactly today's fixed set, not an empty one (which would silently stop all chat-phrase matching). -> Mitigation: default the field's value at the C# property-initializer level (same pattern already used for every other `Configuration` default), not via a migration step, so deserialization naturally leaves it at the safe default when absent from an old saved file.
