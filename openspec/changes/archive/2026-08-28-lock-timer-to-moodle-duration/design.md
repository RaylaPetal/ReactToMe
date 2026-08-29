## Context

See proposal.md - Why. `ReactionTrigger.Duration` today always drives `ActiveEffectRegistry`'s Glamourer-revert timer; there is no permanent/no-expiration state.

Moodles' only duration-bearing IPC calls (`GetStatusInfoV2`, `GetStatusInfoListV2`, `GetStatusManagerInfo*`) return a `MoodlesStatusInfo` tuple containing two Moodles-private enum types (`StatusType`, `ChainTrigger`). Dalamud's IPC binding (via ECommons' EzIPC, built on `IDalamudPluginInterface.GetIpcSubscriber<...>`) requires the subscriber's generic type arguments to be the exact same CLR types as the provider's in order to unbox the unmarshaled result; that isn't possible without a compile-time reference to Moodles' actual assembly, and no published shared-types package exists for Moodles (unlike `Glamourer.Api`, which ReactToMe already depends on). The duration-free `GetRegisteredMoodlesV2` call — the one currently used by `MoodlesIpc.GetMoodles()` — remains the only Moodles listing call ReactToMe can safely use.

This was discovered while implementing the original design (which assumed Moodles' duration could be read live via IPC); that assumption didn't hold, so the mechanism below replaces it entirely with local-only state.

## Goals / Non-Goals

**Goals:**
- Let a trigger's own revert timer represent "never expires," entirely through local state (no Moodles IPC).
- Preserve existing numeric-duration behavior unchanged when the new option is off.

**Non-Goals:**
- Reading, syncing, or validating a Moodle's actual configured duration or no-expiration flag from Moodles — not currently feasible (see Context). The option is a manual, user-asserted mirror of what the user separately configured in Moodles.
- Any change to `MoodlesIpc.cs` or the Moodles IPC surface used.

## Decisions

1. **No IPC involvement at all.** Given the type-matching blocker, the feature is implemented entirely with local state: a new boolean on `ReactionTrigger`, consumed only by `ActiveEffectRegistry`. Alternatives considered: referencing Moodles' compiled DLL directly to get the real enum types (rejected — fragile across Moodles versions/machines with no published API contract to pin against, and a bigger change than this feature warrants); asking Moodles' author to expose a primitives-only duration call (worth pursuing separately upstream, but not something this change can depend on).
2. **`ActiveEffect.ExpiresAtUtc` becomes nullable (`DateTime?`)** rather than using a sentinel like `DateTime.MaxValue`, keeping "no timer" explicit at the type level. `Tick()`'s expiry sweep skips entries with a null expiry.
3. **Gate the checkbox's visibility on `MoodleGuid` being set**, consistent with the feature's purpose (mirroring a permanent Moodle), even though the underlying mechanism no longer touches Moodles at runtime — this keeps the option meaningfully scoped to the plugin's Moodles integration rather than becoming a general-purpose "permanent effect" toggle.

## Risks / Trade-offs

- [Risk] The checkbox is a manual mirror with no verification — a user can enable "No expiration" for a trigger whose Moodle actually does expire, or leave it off for one that doesn't, and ReactToMe has no way to detect the mismatch → Mitigation: the checkbox's tooltip explicitly states this is manual and not auto-synced.
- [Risk] Changing `ActiveEffect.ExpiresAtUtc` to nullable touches internal state shape → Mitigation: besides `ActiveEffectRegistry` itself (`Tick()`, `RevertAll()`), `Windows/MainWindow.cs` also reads it to show remaining time in the active-effects list; both are updated in the same change.
