## Why

A trigger's effect-removal timer today is always a fixed numeric `Duration` — there is no way to mark a trigger as never auto-reverting, even when it's paired with a Moodle the user has deliberately set to never expire in Moodles. This makes it easy to end up with a mismatched setup: the Moodle status stays applied forever while ReactToMe's own Glamourer-revert timer silently reverts the paired design on its separate numeric schedule, defeating the point of pairing a permanent look with a permanent buff/debuff.

## What Changes

- Add a per-trigger "No expiration" option, available only when the trigger has a Moodle selected.
- When enabled, the trigger's applied effects are never auto-reverted by ReactToMe — they stay until manually cleared, mirroring the semantics of a Moodle configured with no expiration.
- When disabled (the default), behavior is unchanged: the trigger's manually-set `Duration` still governs auto-revert, exactly as today.
- No Moodles IPC changes. Investigated during planning: Moodles does not expose a safe way for an external plugin to read a saved status's configured duration or no-expiration flag. Its only duration-bearing IPC calls (`GetStatusInfoV2`, `GetStatusInfoListV2`, `GetStatusManagerInfo*`) return a tuple containing two Moodles-private enum types that can't be matched from outside the Moodles assembly without a published shared-types package (unlike Glamourer, which ships `Glamourer.Api`); calling them as declared would fail at the Dalamud IPC boundary. The user is expected to manually mirror their Moodle's own no-expiration setting via this checkbox — ReactToMe cannot detect or auto-sync it.
- `ActiveEffect.ExpiresAtUtc` becomes nullable to represent "never expires" as a first-class state instead of a numeric duration.

## Capabilities

### New Capabilities
- `moodle-duration-lock`: per-trigger "No expiration" option (available when a Moodle is selected) that suppresses ReactToMe's own auto-revert timer, for pairing with a Moodle the user has manually set to never expire.

### Modified Capabilities
- (none — no existing capability specs exist yet in this project)

## Impact

- `ReactToMe/Triggers/ReactionTrigger.cs` — new `NoExpiration` boolean field, relevant only when `MoodleGuid` is set.
- `ReactToMe/Effects/ActiveEffectRegistry.cs` — `ActiveEffect.ExpiresAtUtc` becomes `DateTime?`; `Apply()` sets it to `null` when the trigger's no-expiration option is enabled; `Tick()` and `RevertAll()` updated for the nullable expiry.
- `ReactToMe/Windows/ConfigWindow.cs` — new checkbox next to the Duration field, disabled/hidden without a Moodle selected, with a tooltip explaining this is a manual mirror of Moodles' own setting, not auto-synced.
- No changes to `ReactToMe/Ipc/MoodlesIpc.cs` — extending it for duration data was investigated during planning and found infeasible without a Moodles-published shared types package (see design.md).
