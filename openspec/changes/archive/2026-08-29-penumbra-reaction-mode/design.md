## Context

See proposal.md for motivation. Today `ReactionTrigger.PenumbraStages` is a `List<PenumbraStageThreshold>` (Threshold, ModDirectory, ModName, OptionGroupName, OptionName); an empty list means no Penumbra reaction, any non-empty list is drawn as the tabbed, threshold-driven editor from the `tabbed-penumbra-stages` change. `ActiveEffectRegistry.ResolveStage`/`ApplyPenumbraStage` resolve the active stage as "the greatest threshold at or below the current fire count," which already degenerates correctly to "the one stage, applied from the first fire" when there's exactly one entry with `Threshold == 1` — that's precisely what Single mode needs, so no application-logic changes are required.

The wrinkle is disambiguating *why* a trigger has exactly one stage: a deliberate Single-mode reaction (always threshold 1, hidden from the user) versus a legitimate Staged config that happens to have only one stage configured, possibly at a non-1 threshold (e.g. "don't apply until the 5th stack, then apply and stop escalating"). Count alone can't tell these apart, so the mode has to be its own stored field.

## Goals / Non-Goals

**Goals:**
- Let a trigger declare an explicit Penumbra reaction mode (None/Single/Staged) that gates which fields the UI shows.
- Preserve every existing trigger's behavior exactly as-is after upgrading, with no manual migration step.
- Reuse the existing `PenumbraStages` data and application/matching code unchanged.

**Non-Goals:**
- No change to how a resolved stage is applied, disabled, or reverted (`PenumbraIpc`, `ActiveEffectRegistry`).
- No change to the tabbed Staged-mode editor's own behavior, beyond gating when it's shown.

## Decisions

- **New field, not inferred purely from list length**: add `PenumbraReactionMode { None, Single, Staged }` to `ReactionTrigger`, defaulting to `None`. Rejected alternative: infer mode purely from `PenumbraStages.Count` (0/1/2+) — this can't distinguish a deliberate Single reaction from a legitimate one-stage Staged config at a non-1 threshold, silently corrupting the latter's threshold to 1 the first time the UI renders it.
- **Back-compat via effective-mode resolution, not a data migration**: rather than rewriting old config files, compute the mode actually used as: if the stored mode is `None` (the zero-value every pre-existing config will deserialize to) *and* `PenumbraStages.Count > 0`, treat it as `Staged`; otherwise use the stored value as-is. This is computed at the point the trigger is drawn/matched, self-healing old configs without a version bump or explicit migration pass. The very first time an old trigger's detail pane is drawn, its resolved `Staged` mode gets written back to `PenumbraReactionMode` so the ambiguity doesn't need re-resolving on every read.
- **Single mode is a presentation constraint on the same list, not a separate field set**: Single mode still stores its one mod/option as `PenumbraStages[0]`, with `Threshold` forced to `1` and hidden from the UI, rather than adding parallel `PenumbraSingle*` fields. This keeps `ResolveStage`/`ApplyPenumbraStage` completely untouched — Single is just "Staged with the threshold field locked and the tab bar suppressed."
- **Mode-switch data carryover**: Single -> Staged keeps the one entry and un-hides its threshold (still `1`, now editable); Staged -> Single keeps only `PenumbraStages[0]` (dropping the rest) and re-locks its threshold to `1`; switching to None clears the list entirely. Chosen over silently preserving everything (which would let inactive stage data linger invisibly) or blocking the switch until the user manually cleans up (extra friction for a rare, low-stakes action).

## Risks / Trade-offs

- [Risk] A user relying on a one-stage Staged config at a non-1 threshold, saved before this change, will *not* be silently reinterpreted — the effective-mode back-compat rule always resolves any non-empty `PenumbraStages` to Staged, never Single, so this case is safe by construction.
- [Risk] Switching Staged -> Single silently discards stages beyond the first with no undo. -> Mitigation: this mirrors the already-accepted behavior of removing a stage tab (task 1.2 of `tabbed-penumbra-stages`); no new data-loss pattern is introduced, and switching back to Staged never restores the discarded stages, which is an acceptable trade-off for a rarely-used mode switch.
