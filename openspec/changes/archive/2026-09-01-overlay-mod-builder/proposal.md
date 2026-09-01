## Why

An earlier attempt (`penumbra-texture-overlay`, reverted) tried to apply imported overlay images as a live, temporary Penumbra redirect at trigger-fire time. It never became reliable: the redirect needed a manual character redraw to show up, needed maximum priority to reliably beat the user's own body mod, and needed correctly guessing the right target texture among every resource attached to the player (weapons and props use the same generic mesh-part names as the character's own body, and some body mods redirect to entirely non-standard shared paths). Every one of those problems is specific to faking a mod at runtime — none of them exist for a real, permanent Penumbra mod, which Penumbra's own native pipeline already applies, prioritizes, and redraws correctly because that's what it does for every mod, all the time.

This proposes solving it the way `Sebane1/DragAndDropTexturing` (the reference implementation studied while diagnosing the reverted attempt) actually does: bake overlay stages into a real Penumbra mod once, as a deliberate authoring step, then let the trigger system's existing (already working, unmodified) staged-Penumbra-mod reaction apply it exactly like any other pre-built mod.

## What Changes

- Add a new top-level config tab, "Overlay Mod Builder" (peer to Active Effects / Triggers / Settings), for authoring a permanent Penumbra mod from imported overlay images — a workflow entirely separate from configuring a trigger.
- Within it: pick a texture to read from (reusing the searchable, unfiltered live-texture picker already proven during the reverted attempt) — any live-resolving texture, so the user can build on top of whatever body/skin setup they already have, including another overlay mod's own resolved file — capture a pristine snapshot of it once, add any number of named stages each with an imported overlay image, and bake each stage independently against that same pristine snapshot (never against another stage's output, and never against whatever's live at bake time).
- An explicit "Apply" action writes a real Penumbra mod to Penumbra's own mod directory: one option group (single-select) with one option per stage, each option redirecting the fixed Bibo body base texture path (`chara/bibo_mid_base.tex`, always — this tool is scoped to Bibo body overlays only, not an arbitrary target) to that stage's baked file — then registers it with Penumbra via IPC.
- Re-applying later (after editing a stage's image, adding/removing a stage) updates the same mod in place — a stable identity per project, not a new mod each time — and tells Penumbra to reload it.
- **No changes to trigger-firing behavior at all.** The generated mod is used by the existing staged-Penumbra-mod reaction exactly like a user-authored one: picked by mod/option-group/option in the trigger's existing Penumbra stages UI. This proposal adds no new runtime application path, no new IPC calls at fire time, and no new failure modes to the already-working trigger system.
- Given the format-authoring risk is new and unverified, implementation must start with a hard-gated spike: hand-write the smallest possible mod, register it, and get the user's live in-game confirmation that Penumbra loads and can toggle it correctly — before any staged-baking pipeline is built on top of that foundation.

## Capabilities

### New Capabilities

- `overlay-mod-builder`: lets the user author a permanent, multi-stage Penumbra mod from imported overlay images, baked against a pristine snapshot of a chosen target texture, for use by the existing staged-Penumbra-mod trigger reaction.

### Modified Capabilities

(none — the staged-Penumbra-mod reaction's own behavior is completely unchanged; it just gains a new source of mods to point at)

## Impact

- `ReactToMe/Windows/ConfigWindow.cs`: new "Overlay Mod Builder" tab and its own draw methods, added to the existing top-level tab bar.
- `ReactToMe/Configuration.cs` (or a new model file): a new persisted list of overlay-mod-builder projects, independent of `ReactionTrigger` — target game path, pristine snapshot reference, named stages (each an imported overlay image plus its last-baked/last-applied state), and the project's stable generated mod directory name.
- `ReactToMe/Ipc/PenumbraIpc.cs`: new methods wrapping `GetModDirectory`, `AddMod`, and `ReloadMod`, alongside the existing mod-related IPC already there.
- A new mod-authoring component writing Penumbra's mod file format (`meta.json` plus a single-select option group definition) to disk, referencing `Sebane1/LooseTextureCompilerCore`'s `Json/` classes as the schema reference.
- Reuses, unmodified: the searchable live-texture picker, the `ConvertTextureFile`-based base-texture decode, and the ImageSharp compositing logic already validated during the reverted `penumbra-texture-overlay` attempt (that change's code was removed, but the approach is being re-established here).
- No changes to `ReactToMe/Effects/ActiveEffectRegistry.cs` or the staged-Penumbra-mod reaction's trigger-firing logic — the generated mod is consumed by that existing, unmodified code path.
