## 1. Trigger model and revert-timer logic

- [x] 1.1 Add a `NoExpiration` boolean field to `ReactionTrigger` (`ReactToMe/Triggers/ReactionTrigger.cs`), defaulting to `false`, and verify existing trigger JSON config still deserializes without the new field present (old configs)
- [x] 1.2 Change `ActiveEffect.ExpiresAtUtc` (`ReactToMe/Effects/ActiveEffectRegistry.cs`) from `DateTime` to `DateTime?`, and update `Tick()` to skip entries with a null expiry, verified by a manual test where a no-expiration trigger stays active past its trigger's `Duration` value
- [x] 1.3 In `ActiveEffectRegistry.Apply(trigger)`, set the new effect's `ExpiresAtUtc` to `null` when `trigger.NoExpiration` is true, and to `DateTime.UtcNow + trigger.Duration` otherwise; verify both paths with a manual test (one trigger with the option on, one with it off)
- [x] 1.4 Update `RevertAll()` and the refresh-on-repeat path in `Apply()` for the nullable expiry, verifying a repeat fire of a no-expiration trigger stays non-expiring rather than reusing a stale numeric restart

## 2. Config UI

- [x] 2.1 Add a "No expiration" checkbox in `ConfigWindow.Draw()` (`ReactToMe/Windows/ConfigWindow.cs`), positioned next to the existing Duration input, bound to `trigger.NoExpiration`, and verify it persists via `configuration.Save()` like the other checkboxes in that loop
- [x] 2.2 Disable (or hide) the checkbox when `trigger.MoodleGuid == Guid.Empty`, and grey out/hide the manual Duration input when the option is enabled (Duration is unused in that case); verify by toggling the Moodle picker to "(None)" in the running plugin and observing the checkbox becomes unavailable
- [x] 2.3 Add a tooltip on the new checkbox stating this is a manual mirror of the Moodle's own no-expiration setting in Moodles, not auto-synced, matching the existing tooltip style used for the Duration field

## 3. End-to-end verification

- [x] 3.1 Manually verify the three scenarios in `specs/moodle-duration-lock/spec.md`: the option is only available with a Moodle selected, enabling it suppresses auto-revert until manually cleared, and disabling it (default) preserves today's manual-duration auto-revert behavior
