using System;
using System.Collections.Generic;

namespace ReactToMe.OverlayModBuilder;

/// <summary>One named stage of an <see cref="OverlayModBuilderProject"/>: an imported overlay image, baked
/// against the project's pristine snapshot into that stage's own file inside the generated mod folder
/// (see <see cref="OverlayModWriter"/>). Baking happens only on save and via explicit rebake/recapture —
/// never automatically — so this tracks what the last successful bake used, to tell whether a later apply
/// needs to re-bake this stage.</summary>
[Serializable]
public class OverlayModBuilderStage
{
    /// <summary>Permanent per-stage identity, assigned once and never reused — this stage's own baked
    /// texture/preview files are named from this, not from its current position in the project's stage
    /// list, so removing or reordering stages can never make one stage's files collide with, or be mistaken
    /// for, another's. See <see cref="OverlayModWriter.GetStageRelativeFilePath"/>.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Shown as this stage's Penumbra option name once the project is applied.</summary>
    public string Name { get; set; } = "Stage";

    /// <summary>ReactToMe's own managed copy of the imported overlay image — never the user's original
    /// external file path, so moving or deleting that original elsewhere doesn't break the project. Always
    /// empty for the implicit baseline stage (<see cref="IsBaseline"/>): it has no overlay of its own, only
    /// the pristine snapshot.</summary>
    public string OverlayImagePath { get; set; } = string.Empty;

    /// <summary>Whether this is the implicit "Stage 0" baseline auto-generated from the project's pristine
    /// snapshot with no overlay applied — always at index 0, never user-removable, and the base every other
    /// stage's chain ultimately starts from. See <see cref="OverlayModBuilderService.CaptureSnapshotAsync"/>.</summary>
    public bool IsBaseline { get; set; }

    /// <summary>The overlay image path and pristine-snapshot version the last successful bake used,
    /// compared against this stage's and its project's current values to decide whether it needs baking.
    /// Empty <see cref="LastBakedOverlayImagePath"/> means this stage has never been successfully baked, or
    /// its last bake failed — such a stage is left out of the generated mod entirely (see
    /// <see cref="OverlayModWriter.ApplyProject"/>) rather than applied broken. Not consulted for a baseline
    /// stage, since its overlay image is always (validly) empty — see <see cref="NeedsRebake"/>.</summary>
    public string LastBakedOverlayImagePath { get; set; } = string.Empty;

    public int LastBakedSnapshotVersion { get; set; } = -1;

    /// <summary>The project's <see cref="OverlayModBuilderProject.ModeVersion"/> in effect during this
    /// stage's last successful bake. Not applicable to the baseline stage, since bake mode only changes
    /// which stage a non-baseline stage's bake reads from. Lets a stage detect that the project's bake mode
    /// has changed since it last baked, the same way <see cref="LastBakedSnapshotVersion"/> detects a
    /// recaptured snapshot.</summary>
    public int LastBakedModeVersion { get; set; } = -1;

    /// <summary>The <see cref="OverlayModWriter.StageFileSchemeVersion"/> in effect during this stage's last
    /// successful bake. A code-level change to where/how stage files are laid out on disk invalidates every
    /// already-baked stage the same way a changed overlay image or recaptured snapshot would, but neither of
    /// those fields can ever reflect that — this one exists specifically so bumping the scheme version
    /// forces a re-bake automatically instead of silently leaving stages pointing at a stale location.</summary>
    public int LastBakedFileSchemeVersion { get; set; } = -1;

    /// <summary>Incremented every time this stage successfully bakes — lets whichever stage's bake reads
    /// from this one (the next stage in the chain under <see cref="OverlayModBuilderBakeMode.Chained"/>, or
    /// every non-baseline stage under <see cref="OverlayModBuilderBakeMode.FromBaseline"/> when this is the
    /// baseline) detect that its own base has changed, via <see cref="LastBakedPredecessorVersion"/>.</summary>
    public int BakeVersion { get; set; }

    /// <summary>The base stage's (see <c>OverlayModBuilderService.GetBaseStageIndex</c> — the immediately
    /// preceding stage under <see cref="OverlayModBuilderBakeMode.Chained"/>, or Stage 0 under
    /// <see cref="OverlayModBuilderBakeMode.FromBaseline"/>) <see cref="BakeVersion"/> at the time this
    /// stage last baked. Not applicable to the baseline stage (it has no base stage of its own — it bakes
    /// from the pristine snapshot directly). A mismatch against that base stage's current
    /// <see cref="BakeVersion"/> means it rebaked since, so this stage's own output is now stale too.</summary>
    public int LastBakedPredecessorVersion { get; set; } = -1;

    /// <summary>Whether this stage needs (re-)baking: never successfully baked, its source image has
    /// changed since its last successful bake, the project's pristine snapshot has been recaptured since
    /// then, the project's bake mode has changed since then, the on-disk file scheme itself has changed
    /// since then, or (for a non-baseline stage) its base stage has rebaked since this stage last did.</summary>
    public bool NeedsRebake(int currentSnapshotVersion, int currentModeVersion, int? baseStageBakeVersion)
    {
        if (!IsBaseline && (string.IsNullOrEmpty(LastBakedOverlayImagePath) || LastBakedOverlayImagePath != OverlayImagePath))
            return true;

        if (LastBakedSnapshotVersion != currentSnapshotVersion)
            return true;

        if (!IsBaseline && LastBakedModeVersion != currentModeVersion)
            return true;

        if (LastBakedFileSchemeVersion != OverlayModWriter.StageFileSchemeVersion)
            return true;

        return baseStageBakeVersion.HasValue && LastBakedPredecessorVersion != baseStageBakeVersion.Value;
    }
}

/// <summary>How a non-baseline stage's bake picks its base image. <see cref="Chained"/> is the original,
/// zero-value default so projects saved before this field existed deserialize into unchanged behavior —
/// see <see cref="OverlayModBuilderProject.BakeMode"/>.</summary>
public enum OverlayModBuilderBakeMode
{
    /// <summary>Every stage composites on top of the immediately preceding stage's own baked output.</summary>
    Chained = 0,

    /// <summary>Every stage composites directly on top of Stage 0's (the baseline's) own baked output,
    /// regardless of its position in the stage list.</summary>
    FromBaseline,
}

/// <summary>
/// A permanent-Penumbra-mod authoring project: pick a target texture, capture a pristine snapshot of it
/// once, add any number of named stages each with an imported overlay image, and apply to write/update a
/// real Penumbra mod with one single-select option per stage. Independent of any <see cref="Triggers.ReactionTrigger"/> —
/// a trigger's existing staged-Penumbra-mod reaction picks the generated mod's group/options afterward,
/// exactly like any other pre-built mod.
/// </summary>
[Serializable]
public class OverlayModBuilderProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = "New Overlay Project";

    /// <summary>The picked candidate's own reported game path — purely cosmetic bookkeeping so the target
    /// picker re-selects the same entry when the project is reopened. Never used to decide what the
    /// generated mod redirects: that is always the fixed <see cref="OverlayModWriter.RedirectGamePath"/>, no
    /// exceptions, since this tool only ever builds staged overlays on top of the Bibo body base texture.
    /// Some picked candidates report their own absolute file path here instead of a clean virtual one (e.g.
    /// a mod that rewrites a material to reference its own file directly) — that's fine, this field is never
    /// interpreted as a real game path, only compared back against the candidate list for re-selection.</summary>
    public string SourceGamePath { get; set; } = string.Empty;

    /// <summary>The actual file this project reads pixel data from when (re)capturing its pristine
    /// snapshot — the resolved path of whatever candidate was picked. This is the only thing a project
    /// actually needs to know to build itself; what it becomes once applied (always
    /// <see cref="OverlayModWriter.RedirectGamePath"/>) is fixed and unrelated to this value. Empty = no
    /// target chosen yet.</summary>
    public string TargetActualPath { get; set; } = string.Empty;

    /// <summary>Path to the pristine snapshot of <see cref="TargetActualPath"/>, captured once (or
    /// re-captured explicitly) and reused as the base for every stage's bake — never the live texture at
    /// bake time, and never another stage's output. Empty = not captured yet; stages cannot be baked until
    /// it is.</summary>
    public string PristineSnapshotPath { get; set; } = string.Empty;

    /// <summary>Incremented every time the snapshot is recaptured, so a stage baked against an older
    /// snapshot can tell it's now stale even though <see cref="PristineSnapshotPath"/> itself didn't
    /// change (recapturing overwrites the same file in place).</summary>
    public int SnapshotVersion { get; set; }

    /// <summary>How this project's non-baseline stages pick their base image — see
    /// <see cref="OverlayModBuilderBakeMode"/>. <see cref="OverlayModBuilderBakeMode.Chained"/> is the
    /// zero-value default specifically so projects saved before this field existed deserialize into their
    /// original (chained) behavior unchanged. Changed only via <see cref="SetBakeMode"/>, never assigned
    /// directly, so <see cref="ModeVersion"/> always reflects every actual change.</summary>
    public OverlayModBuilderBakeMode BakeMode { get; set; } = OverlayModBuilderBakeMode.Chained;

    /// <summary>Incremented every time <see cref="BakeMode"/> actually changes (via <see cref="SetBakeMode"/>),
    /// so an already-baked non-baseline stage can tell its baked output was produced under a since-changed
    /// mode even though nothing else about it changed — mirrors <see cref="SnapshotVersion"/>'s role for
    /// snapshot recapture.</summary>
    public int ModeVersion { get; set; }

    /// <summary>Sets <see cref="BakeMode"/> and bumps <see cref="ModeVersion"/> if it actually changed. The
    /// only supported way to change bake mode — assigning <see cref="BakeMode"/> directly (as JSON
    /// deserialization does when loading a saved project) intentionally leaves <see cref="ModeVersion"/>
    /// alone, since that's not a user-initiated mode change.</summary>
    public void SetBakeMode(OverlayModBuilderBakeMode mode)
    {
        if (BakeMode == mode)
            return;

        BakeMode = mode;
        ModeVersion++;
    }

    /// <summary>Whether this project has been applied at least once — determines
    /// whether the next apply calls <c>AddMod</c> (first time) or <c>ReloadMod</c> (every time after).</summary>
    public bool IsApplied { get; set; }

    /// <summary>This project's generated mod's priority in the local player's active collection — Penumbra's
    /// own inter-mod conflict resolution, applied via <see cref="Ipc.PenumbraIpc.SetModPriority"/> whenever
    /// the project is applied or its mod is recreated. Defaults to 0, Penumbra's own default for a
    /// newly-registered mod, so a project that never touches this behaves exactly as before this field
    /// existed.</summary>
    public int Priority { get; set; }

    /// <summary>Optional folder path this project's mod is filed under in Penumbra's own mod list (e.g.
    /// "Body"), applied via <see cref="Ipc.PenumbraIpc.SetModPath"/> whenever the project is applied or its
    /// mod is recreated. Empty = leave the mod wherever Penumbra already has it, same as before this field
    /// existed.</summary>
    public string PenumbraFolder { get; set; } = string.Empty;

    public List<OverlayModBuilderStage> Stages { get; set; } = [];
}
