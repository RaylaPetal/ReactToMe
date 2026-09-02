# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ReactToMe is a [Dalamud](https://dalamud.dev) plugin for FINAL FANTASY XIV. It listens for emotes,
job-skill casts, and chat phrases directed at (or by) the local player and reacts by applying a
Glamourer design, a Moodles status, a staged Penumbra mod, a chat message/gesture, or some
combination — then auto-reverts the Glamourer/Penumbra side after a configured duration. It was
scaffolded from Dalamud's `SamplePlugin` template — `README.md` and `.github/workflows/pr-build.yml`
still carry template/SamplePlugin naming and are not authoritative for this repo's actual build.

## Build

There is no `.sln`; build the project directly with the `x64`/`Debug` (or `Release`) configuration
that matches how the game's dev-plugin loader expects the DLL laid out:

```bash
dotnet build ReactToMe/ReactToMe.csproj -c Debug -p:Platform=x64
```

Output: `ReactToMe/bin/x64/Debug/ReactToMe.dll`. `ReactToMe.slnx` (the newer slnx solution format)
pins the project to the `x64` platform already, so `dotnet build ReactToMe.slnx` also works.

The project targets .NET 10 via the `Dalamud.NET.Sdk` and resolves Dalamud from
`~/.xlcore/dalamud/Hooks/dev/` (or `$DALAMUD_HOME` if set) — a local Dalamud/XIVLauncher install is
required to build, since the SDK pulls Dalamud/game-facing assembly references from there.

No test suite exists in this repo.

## Loading in-game

Add the built DLL's path to Dalamud's Dev Plugin Locations (`/xlsettings` → Experimental), then
enable `ReactToMe` under `/xlplugins` → Dev Tools → Installed Dev Plugins. In-game command:
`/reacttome` (toggles the config window), `/reacttome clear` (force-reverts all active effects).

## Architecture

**Event sources → matcher → effect registry**, wired up entirely in `Plugin.cs`:

- Three independent pollers/listeners feed events into `Plugin`:
  - `EmotePoller` (`Emotes/`) — polls for emotes performed near the player.
  - `JobSkillPoller` (`JobSkills/`) — polls for job-skill casts (only actions with a nonzero cast
    time are detectable this way).
  - `ChatMessageListener` (`ChatDetection/`) — subscribes to `IChatGui.ChatMessage` for the
    channel types in `Configuration.WatchedChatChannels`.
- Each event handler in `Plugin` (`OnEmotePerformed`, `OnJobSkillCast`, `OnChatMessageReceived`)
  calls into `TriggerMatcher` (`Triggers/TriggerMatcher.cs`), a pure static matcher that finds the
  first enabled, action-having `ReactionTrigger` whose `TriggerSourceType` and scope/filter fields
  match. A match calls `Plugin.FireReactions`, which is the single path both real events and the
  config UI's "Test Fire" button go through — there is no separate preview code path.
- `ReactionTrigger` (`Triggers/ReactionTrigger.cs`) is the trigger model: it has one
  `TriggerSourceType` (Emote / ChatPhrase / JobSkill) and only that type's source fields are used
  for matching; its action fields (Glamourer design, Moodle, chat message, Penumbra stages,
  gesture) are all independently optional and any subset can be set.
- `ActiveEffectRegistry` (`Effects/ActiveEffectRegistry.cs`) owns the Glamourer/Penumbra side of a
  fired trigger: applying the design/staged mod, tracking `ExpiresAtUtc` per active effect, and
  reverting on a coarse ~1s tick (`Tick()`, called from `Plugin.OnFrameworkUpdate`) or on
  `RevertAll()` (manual clear, or on logout if `Configuration.RevertOnRelog`). Moodles is *not*
  tracked here — a Moodle's own preset duration governs its own expiry, since Moodles exposes no
  IPC to read a status's remaining duration.
  - Non-stacking triggers (`StackMultiple == false`) occupy a single replace-on-restack slot;
    `RefreshOnRepeat` decides whether a repeat fire restarts the timer.
  - Penumbra staging (`PenumbraReactionMode.Staged`) escalates through `PenumbraStages` by fire
    count, capped at `ActiveEffectRegistry.MaxPenumbraFireCount` (20, matching FFXIV's native debuff
    stack cap); `GetEffectivePenumbraMode()` must be used instead of reading the raw enum, since it
    corrects pre-existing configs saved before the mode existed.
- `Ipc/` wraps the three external plugin IPC surfaces this plugin drives: `GlamourerIpc`,
  `MoodlesIpc`, `PenumbraIpc`. All calls are expected to no-op with a chat warning if the target
  plugin isn't installed/loaded — this plugin has no hard dependency on any of them.
- `Configuration.cs` is the serialized (`IPluginConfiguration`) root: a flat `List<ReactionTrigger>`
  plus global settings (watched chat channels, revert-on-relog, new-trigger seed defaults). Saved
  via `configuration.Save()` from the config UI immediately on each edit — there's no separate
  "apply" step.
- `Windows/ConfigWindow.cs` is the single ImGui config window (tabbed: triggers list/editor,
  active effects, settings) — by far the largest file in the project. `Windows/PurpleTheme.cs`
  pushes the plugin's ImGui color theme around all window drawing (`Plugin.DrawWindows`).

**Overlay Mod Builder** (`OverlayModBuilder/`) is a separate, mostly independent subsystem for
building layered-texture Penumbra mods from a base "pristine snapshot" plus named overlay stages
baked on top (`OverlayModBuilderProject`/`OverlayModBuilderStage` model,
`OverlayModBuilderService` orchestrator, `TextureCompositor` for the actual image composition via
ImageSharp, `OverlayModWriter` for writing/registering the generated Penumbra mod). It's driven
from its own section of the config UI and does not go through `TriggerMatcher`/
`ActiveEffectRegistry`. `SpikeTest.cs` holds throwaway manual test entry points reachable via
`/reacttome spiketest[...]` — not part of the normal user-facing flow.

## Conventions specific to this codebase

- XML doc comments are used liberally and are often load-bearing — they frequently explain *why* a
  field/default exists (e.g. backward-compat resolution for configs saved before a field existed),
  not just what it is. Read them before changing a model field's default or removing a field.
- Enums that gained new values after v1 keep their original member as the zero-value default
  specifically so old serialized configs deserialize into the pre-existing behavior; several types
  expose a `GetEffective*()`-style method to resolve this ambiguity rather than trusting the raw
  stored enum value directly (see `ReactionTrigger.GetEffectivePenumbraMode`).
- Feature work in this repo goes through OpenSpec (`openspec/` — proposals, specs, and an archive of
  completed changes under `openspec/changes/archive/`); the `openspec-*` skills/slash-commands
  drive that workflow. The user has said an OpenSpec proposal isn't required for every change,
  so don't assume one is needed unless asked.
