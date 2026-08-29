## 1. Trigger data model

- [x] 1.1 Add a `TriggerSourceType` enum (`Emote`, `ChatPhrase`, `JobSkill`) and field to `ReactionTrigger` (`ReactToMe/Triggers/ReactionTrigger.cs`), defaulting to `Emote`, and verify existing trigger JSON config (emote-only) still deserializes and behaves identically without the field present (old configs)
- [x] 1.2 Add `ChatPhrase` (string), `ChatTriggerSource` (new enum: `SelfTyped`/`AnyoneNearby`, default `AnyoneNearby`), `JobSkillActionId` (uint), `GestureEmoteId` (uint), and `Name` (string) fields to `ReactionTrigger`, all defaulting empty/zero; update `HasAnyAction` to include `GestureEmoteId != 0`
- [x] 1.3 Update `TriggerMatcher.FindMatch` (or split into per-source-type methods it dispatches to) to branch on `TriggerSourceType`, keeping the existing emote path's behavior byte-for-byte identical; verify with existing emote triggers that behavior is unchanged

## 2. Chat-phrase triggers

- [x] 2.1 Create `ChatMessageListener` (mirroring `EmotePoller`'s event shape) subscribing to `IChatGui.ChatMessage`, filtering to Say/Yell/Shout/Tell/Party/Alliance/FreeCompany channel types, and exposing an event with the message text and whether the sender was the local player; verify by logging incoming watched-channel messages in a manual test
- [x] 2.2 Add chat-phrase matching to `TriggerMatcher`'s dispatch: case-insensitive substring match against `ChatPhrase`, gated by `ChatTriggerSource` (self-typed vs. anyone nearby); verify both scopes with a manual test (typing the phrase yourself vs. having another player type it)
- [x] 2.3 Wire `ChatMessageListener` into `Plugin.cs` alongside `EmotePoller`, routing matched chat-phrase triggers through the same `EffectRegistry.Apply`/reaction-firing path already used for emote matches

## 3. Job-skill triggers

- [x] 3.1 Create an action catalog wrapping the relevant Lumina action sheet, exposing action id -> display name for the picker; verify it returns real action names in a manual test
- [x] 3.1a Revised after initial testing: filter the catalog to `IsPlayerAction && !IsPvP && ClassJob.RowId != 0` and group by owning job (`GetJobs()`/`GetActionsForJob()`), instead of one flat list of every Action sheet row — the unfiltered list included NPC/monster actions, PvP variants, and duplicates; add `ReactionTrigger.JobSkillClassJobId` for the job picker's own selection state
- [x] 3.2 Create `JobSkillPoller` (mirroring `EmotePoller`'s per-frame, per-actor last-seen-value diffing) reading `Character.GetCastInfo()->ActionId`/`IsCasting` for every nearby `IPlayerCharacter`, firing an event on a false-to-true `IsCasting` transition; verify by casting a spell with a cast bar and confirming the event fires once at cast start, not repeatedly while casting
- [x] 3.3 Add job-skill matching to `TriggerMatcher`'s dispatch, reusing `TriggerScope` (self/others-targeting-me/anyone) against the cast's source and target; verify all three scopes with a manual test
- [x] 3.4 Wire `JobSkillPoller` into `Plugin.cs` alongside `EmotePoller`, routing matched job-skill triggers through the same reaction-firing path

## 4. Gesture reaction

- [x] 4.1 Extend `EmoteCatalog` to resolve each emote's slash command text (confirm the actual Lumina sheet reference — e.g. `Emote.TextCommand` -> `TextCommand.Command` — against the current Lumina.Excel.Sheets version), and expose only emotes with a resolvable command for the gesture picker
- [x] 4.2 On a trigger fire, when `GestureEmoteId != 0`, send the resolved command through the existing `ChatMessageSender`, sharing that trigger's existing chat-send cooldown/last-sent tracking with the chat-message reaction (not a separate cooldown); verify by configuring both a chat message and a gesture on one trigger and confirming they share one cooldown window
- [x] 4.3 Confirm gesture reactions are not added to `ActiveEffectRegistry` (fire-and-forget, no revert) — verify a trigger with a gesture and a Glamourer design shows the gesture firing once while the Glamourer design still reverts normally on its own timer

## 5. Config UI rework

- [x] 5.1 Add a top-level tab bar to `ConfigWindow` with "Triggers" and "Settings" tabs; move the existing "Movable Config Window" and "Revert active effect on logout" checkboxes into the Settings tab, and verify they still persist via `configuration.Save()`
- [x] 5.1a Revised after initial testing: delete `MainWindow` entirely and add its content (the active-effects list and "Force revert all", minus its now-unneeded "Show Settings" button) as a third "Active Effects" tab, so the plugin presents exactly one window with three tabs (Active Effects, Triggers, Settings); `Plugin.cs`'s `ToggleMainUi`/`ToggleConfigUi` both now target the single `ConfigWindow`
- [x] 5.2 Build the Triggers tab's master list: a filterable list of triggers (filter box + per-trigger label), selecting a trigger to show its detail pane; compute each trigger's label from `Name` if set, else a per-source-type generated label (emote name / chat phrase in quotes / action name); verify filtering narrows the list and selecting a trigger updates the detail pane
- [x] 5.3 Build the detail pane's Source section: a `TriggerSourceType` selector, then only the fields relevant to the selected type (emote+scope / chat phrase+chat-trigger-source / job+skill+scope, the skill list scoped to the chosen job); verify switching source type shows only that type's fields
- [x] 5.4 Build the detail pane's Reactions section: relocate the existing Glamourer design picker, Moodle picker, chat message + cooldown, and Penumbra stages list into this section, and add the new gesture emote picker; verify every relocated control still works and persists exactly as before the rework
- [x] 5.5 Build the detail pane's Timing & Revert section: relocate Duration, revert-target mode + design picker, No-expiration, Refresh-on-repeat, and Stack-multiple into this section; verify every relocated control still works and persists exactly as before the rework
- [x] 5.6 Add hover tooltips to fields whose purpose isn't obvious from their label alone, reusing/relocating the tooltip text already written for the fields being moved, and writing new tooltips for source-type- and gesture-specific fields
- [x] 5.7 Verify the "Refresh lists" button, the emote/design/moodle/mod searchable pickers' search filters, and each control's enabled/disabled gating (e.g. No-expiration requiring a Moodle) all still work identically after the rework

## 6. Bug fix: Penumbra stage application

- [x] 6.1 In `PenumbraIpc.SetStage` (`ReactToMe/Ipc/PenumbraIpc.cs`), capture the `PenumbraApiEc` returned by both `TrySetMod` and `TrySetModSetting`, and log + show the existing chat-warning pattern when either is not `Success`/`NothingChanged`; verify by reproducing the reported scenario (fresh Penumbra permission grant) and confirming a warning now appears if the option-group set is rejected, instead of it failing silently
- [x] 6.2 Revised after user feedback: replace `PenumbraIpc`'s permanent `TrySetMod`/`TrySetModSetting` calls with Penumbra's temporary, player-scoped settings IPC (`SetTemporaryModSettingsPlayer` to apply a stage, `RemoveTemporaryModSettingsPlayer` to clear it), so a staged mod's *permanent* Penumbra configuration is never written — only a temporary overlay that Penumbra itself reverts cleanly; verify a staged mod's permanent settings (checked directly in Penumbra's own UI) are unchanged after the trigger fires and later expires
- [x] 6.3 Revised after further user feedback: `SetTemporaryModSettingsPlayer` requires a `priority` argument on every call, and passing a fixed `0` was silently overwriting a mod's own configured priority (e.g. `4`) for the duration of the override; add `GetCurrentPriority()` (using `GetCollectionForObject` + the permanent, read-only `GetCurrentModSettings`) and pass its result through instead; verify a mod with a non-default configured priority keeps that priority while a ReactToMe stage is active

## 7. End-to-end verification

- [x] 7.1 Manually verify the scenario groups in `specs/trigger-source-types/spec.md`, `specs/gesture-reaction/spec.md`, and `specs/trigger-config-ui/spec.md`: existing emote triggers behave unchanged, chat-phrase triggers fire correctly for both self-typed and anyone-nearby scopes across watched channels only, job-skill triggers fire on cast-time actions per scope, a gesture reaction performs the emote and shares its cooldown with the chat-message reaction, and the reworked config UI's filtering/selection/sections/tooltips/settings-tab all behave as specified
