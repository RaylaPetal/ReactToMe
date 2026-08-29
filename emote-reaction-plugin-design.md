# Emote Reaction Plugin — Design Doc

## 1. Verdict

Yes, this is buildable, and every piece of it has precedent in the existing Dalamud plugin ecosystem:

- **Detecting incoming emotes** (including who performed them) — proven by plugins like AutoEmotion, which already reacts specifically to emotes performed *at* the local player.
- **Applying a Glamourer design programmatically** — Glamourer exposes an IPC surface built exactly for this; multiple plugins (AnimSwapper, various automation tools) drive it externally.
- **Applying/removing a Moodles status programmatically** — Moodles exposes IPC for applying and removing statuses by GUID, and NightmareXIV's plugin collection already ships a rule-based automation tool that drives Glamourer, CustomizePlus, *and* Moodles together.
- **The one genuinely new piece** — a self-cleaning timer that reverts both the design and the moodle after N minutes — isn't something the existing tools do out of the box (they react and apply, but don't manage "this is temporary, revert automatically"). This is your differentiator, and it's a straightforward scheduling problem on top of IPC calls that already exist.

So the architecture below is mostly "orchestration + timers on top of two existing IPC APIs," not new game-hooking territory.

---

## 2. Core concept

```
Incoming emote (e.g. /highfive from PlayerX, targeted at you)
        │
        ▼
Emote Listener (chat log filter, StandardEmote/CustomEmote chat types)
        │
        ▼
Trigger Matcher (emote name, optional source-player filter, optional cooldown)
        │
        ▼
Action Executor
   ├─ Glamourer.ApplyDesign(designId, self)
   └─ Moodles.ApplyOwnStatusByGUID(moodleGuid)
        │
        ▼
Timer registered (duration from trigger config)
        │
   [after duration elapses]
        ▼
Revert Executor
   ├─ Glamourer.RevertToAutomation / RevertState(self)
   └─ Moodles.RemoveOwnStatusByGUID(moodleGuid)
```

## 3. Emote detection

Two viable approaches, in order of recommendation:

### A. Chat log filtering (recommended, matches AutoEmotion's proven approach)
FFXIV prints a chat line for every emote performed near you, in a dedicated chat channel type (`StandardEmote` for built-in emotes, `CustomEmote` for `/em`-style free text). Dalamud's `IChatGui` exposes a message-received event you can filter on these chat types.

- **Pros:** Reliable, already used in production plugins, gives you the emote's display text ("PlayerX gives you a high-five!"), which tells you both the source name and whether it was targeted at you (vs targeted at someone else, vs untargeted).
- **Cons:** You're parsing localized strings, which differ by client language and by whether the emote has a "targeted you" vs "targeted someone else" vs "no target" variant. You'll want to build this against the emote's Lumina sheet data (the `Emote` sheet has the templated text for each case) rather than hardcoding English strings, so it survives language settings.
- **Targeting logic:** the emote text pattern distinguishes "used on you" vs "used on someone else" vs "used with no target" — you only want to fire the trigger for the "used on you" pattern (or optionally "no target" if you want self-triggered reactions too).

### B. Network/actor event hooks (fallback if A proves too fragile)
Emotes also arrive as an actor control event server-side before the chat line is even printed, giving you the numeric emote ID and source/target actor IDs directly — no string parsing, but requires hooking a lower-level Dalamud/game data path and keeping it in sync with game updates.

**Recommendation:** build on A first. It's what AutoEmotion already ships with, it's less likely to break on patches, and your trigger config can be keyed by emote *command* (`/highfive`) which is much friendlier for end users to configure than a numeric ID.

## 4. Trigger configuration model

```csharp
class EmoteTrigger
{
    string Id;                     // internal guid
    string EmoteCommand;           // "/highfive", or Lumina RowId if you go route B
    TargetScope Scope;             // TargetedAtMe | Untargeted | Either
    string? SourcePlayerFilter;    // optional: only fire if from this specific player (name/world)
    bool RequireTargetedAtMe;      // ignore emotes not aimed at you
    Guid GlamourerDesignId;        // design to apply
    Guid MoodleGuid;               // moodle preset to apply
    TimeSpan Duration;             // how long until auto-revert
    bool RefreshOnRepeat;          // if the same trigger fires again mid-timer, reset the clock vs. ignore
    bool StackMultiple;            // allow multiple active trigger instances at once, or single-slot only
    int CooldownSeconds;           // optional: ignore repeats within N seconds (spam guard)
}
```

Config is a simple list of these, edited through an ImGui window (standard Dalamud plugin UI pattern — same as Glamourer/Moodles/AutoEmotion's own config windows). Persisted to plugin config JSON, per-character.

## 5. Timer / expiration engine

This is the part that doesn't exist elsewhere, so it deserves the most design attention.

- **Registry:** a dictionary of `ActiveEffect { TriggerId, ExpiresAtUtc, AppliedDesignId, AppliedMoodleGuid }`.
- **Tick source:** hook Dalamud's `IFramework.Update` (runs every game frame) but only actually check expirations on a coarse interval (e.g. once per second) — no need to check every frame.
- **Persistence across relogs/game restarts:** if the user logs out mid-timer, you have two reasonable options — either (a) persist `ExpiresAtUtc` to disk keyed by character and resume checking on next login, silently applying/reverting based on wall-clock time already elapsed, or (b) treat logout as an implicit revert. (a) is closer to what a habit-forming debuff-timer feature would want; (b) is simpler and avoids "I logged back in five hours later and it insta-reverted" surprise. Worth exposing as a config toggle rather than picking one.
- **Manual override:** always give the user a way to force-revert (a button, or a `/emotereact clear` command) — timers should never trap someone in a state they can't back out of.
- **Conflict handling:** if `StackMultiple = false` and a second trigger fires while one is active, decide via config whether to (i) ignore the new one, (ii) replace it (revert the old, apply the new, restart the timer), or (iii) queue it to apply after the first expires. "Replace" is the least surprising default.
- **Revert correctness:** don't just "clear the moodle/design" — capture what the state *was* before you applied the trigger (or rely on Glamourer's own revert-to-automation/base state and Moodles' remove-by-GUID, which are idempotent and don't need you to snapshot prior state). This avoids clobbering some other glamour/moodle system the user has running independently (like your existing INM-style automation plugin).

## 6. IPC integration notes

- **Glamourer:** call via its published IPC subscriber pattern (same mechanism CustomizePlus/AnimSwapper use) — apply a design by GUID to your own character, and on revert either reapply the character's "base" automation state or explicitly revert to game state, depending on how you want it to interact with any other automation (like your existing condition-driven plugin) running at the same time. Worth deciding up front whether this plugin **replaces** layers temporarily (push/pop a state) or **coexists** as just another automation rule — push/pop is cleaner and avoids fighting your other plugin for control.
- **Moodles:** apply and remove by preset GUID via its IPC (`ApplyOwnStatusByGUID` / `RemoveOwnStatusByGUID` style calls — exact method names to confirm against the current Moodles IPC docs when you scaffold the project, since IPC surfaces do get renamed across API levels). Moodles already natively supports timed statuses in its own preset system — worth checking whether you even need your own timer for the *moodle* half, or whether you can set the moodle's own built-in duration and only need to self-manage the *Glamourer* half's expiration. That would cut your custom timer scope roughly in half.
- **IPC availability guard:** both calls should no-op gracefully (with a chat warning) if Glamourer/Moodles aren't installed/loaded — don't hard-crash your plugin on a missing dependency.

## 7. Build plan (incremental)

1. **Scaffold** — clone the standard SamplePlugin template, get it loading via dev plugin location (same flow you'd use for your existing plugin).
2. **Emote listener only** — log every incoming targeted emote to chat/console, no actions yet. Validates detection before anything else.
3. **Static single trigger** — hardcode one emote → one Glamourer design + one Moodle, no timer, no UI. Validates the two IPC calls work.
4. **Add the timer** — auto-revert after a hardcoded duration.
5. **Config UI + persistence** — replace hardcoded trigger with the editable list model from §4.
6. **Multi-trigger + conflict handling** — stacking/replace/cooldown logic from §5.
7. **Polish** — manual clear command, relog persistence toggle, IPC-missing guards.

## 8. Open questions to settle before coding

- Do you want this to fire on emotes from *any* nearby player, or only ones on a friends/whitelist? (Spam/griefing consideration — someone could `/highfive` you repeatedly to keep re-triggering a debuff you don't want.)
- Should the cooldown/whitelist question above be a hard requirement before v1, given the public-facing griefing angle?
- Do you want moodle duration to be sourced from Moodles' own built-in timer (simpler, per §6) or fully self-managed (more control, more code)?
- Push/pop vs. coexist with your existing condition-driven Glamourer automation plugin — this affects both projects' architecture, so worth deciding once rather than per-project.
