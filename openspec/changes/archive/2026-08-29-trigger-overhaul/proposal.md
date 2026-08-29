## Why

The trigger config screen has one flat, ever-growing list of fields per trigger (currently ~15 controls stacked vertically in a single `CollapsingHeader`), and every new capability added to this plugin (revert-target, moodle-duration-lock, penumbra-mod-staging) has made that worse. It also only supports one way to detect a reaction (an emote performed nearby) and a fixed set of reaction types. This change does four things together because the UI rework is what makes the other three survivable: without reorganizing the screen first, adding two more trigger sources and a new reaction type would make an already-unwieldy screen worse.

It also fixes two real problems found in the shipped `penumbra-mod-staging` capability: a trigger's staged mod gets enabled (and prompts Penumbra's permission dialog) but the option group doesn't reliably get set to stage 1, and — more importantly — the original implementation wrote directly to the mod's *permanent* Penumbra configuration instead of a temporary, player-scoped overlay, meaning a crash, an unexpected shutdown, or simply disabling ReactToMe could leave a mod stuck enabled in the user's real, persisted settings.

## What Changes

### 1. Config UI rework
- Replace the current flat per-trigger field list with a master-detail layout inside the existing config window: a filterable list of triggers on the left, the selected trigger's fields grouped into labeled sections (Source, Reactions, Timing & Revert) on the right, styled after Moodles' own config UI (filter box, sectioned rows, hover tooltips via small info affordances).
- Replace the window's current ad hoc top checkboxes (Movable Config Window, Revert active effect on logout) with a proper tab bar: a "Triggers" tab (the master-detail view above) and a "Settings" tab. This reorganizes the existing config window — it does not add a third window. `MainWindow`'s lightweight active-effects glance view and toggle button are unaffected.
- Add an optional per-trigger display name for the left-list label, falling back to an auto-generated label per source type (e.g. an emote's name, a chat phrase in quotes, a skill's name) when not set.

### 2. Chat-phrase triggers
- A trigger's source can now be a chat phrase instead of an emote: a free-form, case-insensitive substring match against incoming chat messages (e.g. "spank react"), independent per trigger — no fixed suffix convention.
- Each chat-phrase trigger chooses whether it fires only when the local player types the phrase themselves, or when anyone nearby says it (mirroring the self/others distinction emote triggers already have, without emotes' "targeting" concept, which doesn't apply to chat).
- Watches a fixed, non-configurable set of chat channels for v1 (Say, Yell, Shout, Tell, Party, Alliance, Free Company).

### 3. Job-skill triggers
- A trigger's source can now be a job action (skill/spell) being cast, detected the same way emotes already are — reading a per-character struct field every frame and diffing against the last-seen value, extended to `Character.CastInfo.ActionId`/`IsCasting`, which the game populates for any nearby character with a visible cast bar (self or others, no extra hook required for either).
- Reuses the existing self/others/anyone scope, since a cast has a real in-game target the same way an emote does.
- **Scoped to cast-time actions only** (actions with a cast bar — most spells). Instant actions (most weaponskills and abilities, which have no cast time and never populate `CastInfo`) are explicitly out of scope for this change; detecting those would need a heavier network-effect hook, a separate investigation.

### 4. Gesture reaction
- A trigger can now react by having the local player perform an emote, alongside the existing Glamourer design, Moodle, chat message, and Penumbra stage reactions.
- Implemented by resolving the chosen emote's own slash command (from the same Lumina data already used for the emote picker) and sending it through the existing chat-submission pipe already used for the chat-message reaction — no new IPC or hook.
- Shares that same per-trigger send cooldown with the chat-message reaction, since both go through the same chat-spam-throttle-avoidance mechanism.

### 5. Bug fix: Penumbra stage application is unreliable and permanently mutates config
- `PenumbraIpc` currently drives Penumbra's *permanent* mod-settings IPC (`TrySetMod`/`TrySetModSetting`) to enable a mod and set its option group — a real, persisted write to the user's own Penumbra configuration, not something scoped to ReactToMe or reverted when ReactToMe clears the effect. It also ignores the `PenumbraApiEc` result of both calls, so a rejected call (for example, a first-time IPC permission grant not finished before the very next call fires) silently does nothing instead of surfacing a warning.
- Fix: switch to Penumbra's temporary-settings IPC, scoped to the local player, for both enabling+setting a stage and clearing it — the mod's permanent configuration is never touched, and clearing a trigger's override restores the mod to whatever its permanent configuration already was (not a hard-coded disabled state). Also check and surface a non-success `PenumbraApiEc` result from either call via the existing IPC-failure chat-warning pattern, so a rejection is visible instead of silent.
- This is a real requirement gap in the already-shipped `penumbra-mod-staging` spec, not just an implementation defect — that spec never stated a permanent-vs-temporary guarantee at all. This change adds that requirement explicitly (see Modified Capabilities below).

## Capabilities

### New Capabilities
- `trigger-source-types`: a trigger has exactly one source type (emote, chat phrase, or job skill), each with its own matching semantics; generalizes the current emote-only source.
- `gesture-reaction`: a trigger can react by performing an emote, independent of its other reaction types.
- `trigger-config-ui`: the config screen presents triggers as a filterable master-detail list with sectioned, tooltipped fields, organized under a tab bar that also hosts plugin-wide settings.

### Modified Capabilities
- `penumbra-mod-staging`: adds a requirement that staged mod changes are always temporary and player-scoped, never a write to the mod's permanent configuration — a real gap in the original spec, not just an implementation fix.

## Impact

- `ReactToMe/Triggers/ReactionTrigger.cs` — add `TriggerSourceType` (Emote/ChatPhrase/JobSkill) as the source discriminator; add `ChatPhrase`/`ChatTriggerSource` fields; add `JobSkillActionId` field (reusing existing `Scope`); add `GestureEmoteId` field; add an optional `Name` field. Existing `EmoteId`/`Scope` fields are unchanged, just gated on `TriggerSourceType == Emote`.
- `ReactToMe/Triggers/TriggerMatcher.cs` — branch matching logic per `TriggerSourceType` instead of assuming an emote source.
- `ReactToMe/Emotes/EmotePoller.cs` — unchanged; a new `JobSkillPoller` (or similarly-scoped new poller class) is added alongside it using the same per-actor last-seen-value diffing pattern.
- `ReactToMe/Emotes/EmoteCatalog.cs` — extend to also resolve each emote's slash command text (for the gesture reaction and its picker).
- New action/skill catalog (mirrors `EmoteCatalog`, wrapping the relevant Lumina action sheet).
- `ReactToMe/Actions/ChatMessageSender.cs` — reused as-is for sending a resolved gesture command; no interface change expected.
- New chat-message listener wired into `Plugin.cs` (via `IChatGui`, not used for incoming messages today) for chat-phrase trigger detection.
- `ReactToMe/Ipc/PenumbraIpc.cs` — switch from the permanent `TrySetMod`/`TrySetModSetting` IPC to Penumbra's temporary, player-scoped settings IPC (`SetTemporaryModSettingsPlayer`/`RemoveTemporaryModSettingsPlayer`), and check/surface `PenumbraApiEc` results from both instead of ignoring them.
- `ReactToMe/Windows/ConfigWindow.cs` — substantial rework: tab bar, master-detail layout, sectioned fields per trigger. `ReactToMe/Windows/MainWindow.cs` unaffected.
