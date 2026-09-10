using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using ReactToMe.Ipc;
using ReactToMe.Triggers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReactToMe.OverlayModBuilder;

/// <summary>
/// Orchestrates an overlay-mod-builder project: capturing its pristine snapshot, baking individual stages
/// against that snapshot, and applying (bake-what's-needed, then write/register the mod). This is the
/// entry point the config UI (task 6) calls into — it never talks to <see cref="PenumbraIpc"/>,
/// <see cref="TextureCompositor"/>, or <see cref="OverlayModWriter"/> directly.
/// </summary>
public sealed class OverlayModBuilderService
{
    private readonly PenumbraIpc penumbraIpc;
    private readonly TextureCompositor compositor;
    private readonly OverlayModWriter writer;
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;

    public OverlayModBuilderService(PenumbraIpc penumbraIpc, TextureCompositor compositor, OverlayModWriter writer, IPluginLog log, IChatGui chatGui)
    {
        this.penumbraIpc = penumbraIpc;
        this.compositor = compositor;
        this.writer = writer;
        this.log = log;
        this.chatGui = chatGui;
    }

    /// <summary>Path of the composited-preview PNG saved alongside a stage's baked <c>.tex</c> file, so the
    /// config UI can show a thumbnail of what was actually composited without needing to decode Penumbra's
    /// own .tex format itself. Null if Penumbra's mod directory can't currently be resolved, or the
    /// project has no target texture chosen yet.</summary>
    public string? GetStagePreviewPath(OverlayModBuilderProject project, OverlayModBuilderStage stage)
    {
        if (project.TargetActualPath.Length == 0)
            return null;

        var modFolder = writer.GetModFolderPath(project.Id);
        return modFolder == null ? null : GetStagePreviewFilePath(modFolder, stage.Id);
    }

    private static string GetStagePreviewFilePath(string modFolder, Guid stageId) =>
        Path.ChangeExtension(OverlayModWriter.ToDiskPath(modFolder, OverlayModWriter.GetStageRelativeFilePath(stageId)), ".png");

    /// <summary>Deletes a stage's own baked texture and preview files, if they exist. Called when a stage
    /// is removed from a project — since a stage's file identity is now permanently tied to its own
    /// <see cref="OverlayModBuilderStage.Id"/> (never reused by any other stage), this is purely disk
    /// hygiene rather than a correctness fix, but an orphaned baked file otherwise lingers forever. A
    /// missing file is a no-op, not an error.</summary>
    public void DeleteStageFiles(OverlayModBuilderProject project, OverlayModBuilderStage stage)
    {
        var modFolder = writer.GetModFolderPath(project.Id);
        if (modFolder == null)
            return;

        var stageFilePath = OverlayModWriter.ToDiskPath(modFolder, OverlayModWriter.GetStageRelativeFilePath(stage.Id));
        TryDeleteFile(stageFilePath);
        TryDeleteFile(Path.ChangeExtension(stageFilePath, ".png"));
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: failed to delete {Path} while cleaning up a removed stage", path);
        }
    }

    /// <summary>Resolves which stage a non-baseline stage at <paramref name="stageIndex"/> bakes its base
    /// image from: the immediately preceding stage under <see cref="OverlayModBuilderBakeMode.Chained"/>, or
    /// always Stage 0 under <see cref="OverlayModBuilderBakeMode.FromBaseline"/>. Not meaningful for
    /// <paramref name="stageIndex"/> 0 itself (the baseline stage), which bakes from the pristine snapshot
    /// directly regardless of bake mode — callers must not invoke this for that index.</summary>
    public static int GetBaseStageIndex(OverlayModBuilderProject project, int stageIndex) =>
        project.BakeMode == OverlayModBuilderBakeMode.FromBaseline ? 0 : stageIndex - 1;

    private static string GetSnapshotPath(Guid projectId) =>
        Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "OverlayModBuilderSnapshots", $"{projectId:N}.png");

    /// <summary>Captures (or explicitly recaptures) the pristine snapshot of a project's target texture,
    /// reading whatever is currently resolving for it. Bumps <see cref="OverlayModBuilderProject.SnapshotVersion"/>
    /// so every stage can tell it's now stale even though the snapshot file's path doesn't change.</summary>
    public async Task<bool> CaptureSnapshotAsync(OverlayModBuilderProject project)
    {
        try
        {
            if (project.TargetActualPath.Length == 0)
            {
                chatGui.PrintError("[ReactToMe] Overlay mod builder: pick a texture to read from before capturing a snapshot.");
                return false;
            }

            using var baseImage = await compositor.ReadBaseTextureAsync(OverlayModWriter.RedirectGamePath, project.TargetActualPath);
            if (baseImage == null)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: could not read the base texture from {project.TargetActualPath} — check /xllog for details.");
                return false;
            }

            var snapshotPath = GetSnapshotPath(project.Id);
            var snapshotDirectory = Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrEmpty(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            baseImage.SaveAsPng(snapshotPath);

            project.PristineSnapshotPath = snapshotPath;
            project.SnapshotVersion++;

            if (project.Stages.Count == 0 || !project.Stages[0].IsBaseline)
                project.Stages.Insert(0, new OverlayModBuilderStage { Name = "Stage 0 (baseline)", IsBaseline = true });

            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: unhandled exception capturing snapshot for project {ProjectId}", project.Id);
            chatGui.PrintError($"[ReactToMe] Overlay mod builder: unexpected error capturing snapshot ({ex.GetType().Name}: {ex.Message}) — check /xllog for details.");
            return false;
        }
    }

    /// <summary>Bakes one stage: the baseline stage (index 0) re-emits the project's pristine snapshot with
    /// no overlay; every stage after it composites its own overlay on top of the immediately preceding
    /// stage's own baked preview (never the live texture, never any stage other than its direct
    /// predecessor) — writing the result directly into that stage's file inside the project's eventual mod
    /// folder.</summary>
    public async Task<bool> BakeStageAsync(OverlayModBuilderProject project, OverlayModBuilderStage stage)
    {
        try
        {
            if (project.TargetActualPath.Length == 0)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: \"{project.DisplayName}\" has no texture chosen to read from.");
                return false;
            }

            if (project.PristineSnapshotPath.Length == 0 || !File.Exists(project.PristineSnapshotPath))
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: capture \"{project.DisplayName}\"'s pristine snapshot before baking a stage.");
                return false;
            }

            if (!stage.IsBaseline && (stage.OverlayImagePath.Length == 0 || !File.Exists(stage.OverlayImagePath)))
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: stage \"{stage.Name}\" has no overlay image yet.");
                return false;
            }

            var modFolder = writer.GetModFolderPath(project.Id);
            if (modFolder == null)
            {
                chatGui.PrintError("[ReactToMe] Overlay mod builder: could not resolve Penumbra's mod directory.");
                return false;
            }

            var stageIndex = project.Stages.IndexOf(stage);
            if (stageIndex < 0)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: stage \"{stage.Name}\" is no longer part of this project.");
                return false;
            }

            string baseImagePath;
            if (stage.IsBaseline)
            {
                baseImagePath = project.PristineSnapshotPath;
            }
            else
            {
                var baseStage = project.Stages[GetBaseStageIndex(project, stageIndex)];
                baseImagePath = GetStagePreviewFilePath(modFolder, baseStage.Id);
                if (!File.Exists(baseImagePath))
                {
                    chatGui.PrintError($"[ReactToMe] Overlay mod builder: stage \"{stage.Name}\" needs \"{baseStage.Name}\" baked first — missing {baseImagePath}.");
                    return false;
                }
            }

            var relativeFilePath = OverlayModWriter.GetStageRelativeFilePath(stage.Id);
            var stageFilePath = OverlayModWriter.ToDiskPath(modFolder, relativeFilePath);

            var stageFileDirectory = Path.GetDirectoryName(stageFilePath);
            if (!string.IsNullOrEmpty(stageFileDirectory))
                Directory.CreateDirectory(stageFileDirectory);

            using var baseImage = Image.Load<Rgba32>(baseImagePath);
            using var composited = stage.IsBaseline
                ? TextureCompositor.Baseline(baseImage)
                : TextureCompositor.Composite(baseImage, stage.OverlayImagePath);

            try
            {
                composited.SaveAsPng(Path.ChangeExtension(stageFilePath, ".png"));
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Overlay mod builder: failed to save preview for stage {StageName}", stage.Name);
            }

            var rgbaBytes = new byte[composited.Width * composited.Height * 4];
            composited.CopyPixelDataTo(rgbaBytes);

            var success = await penumbraIpc.BakeTextureAsync(rgbaBytes, composited.Width, stageFilePath);
            if (!success)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: failed to bake stage \"{stage.Name}\" — check /xllog for details.");
                return false;
            }

            if (!File.Exists(stageFilePath))
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: ConvertTextureData reported success but {stageFilePath} does not exist afterward.");
                return false;
            }

            stage.LastBakedOverlayImagePath = stage.OverlayImagePath;
            stage.LastBakedSnapshotVersion = project.SnapshotVersion;
            stage.LastBakedModeVersion = project.ModeVersion;
            stage.LastBakedFileSchemeVersion = OverlayModWriter.StageFileSchemeVersion;
            stage.LastBakedPredecessorVersion = stageIndex > 0 ? project.Stages[GetBaseStageIndex(project, stageIndex)].BakeVersion : -1;
            stage.BakeVersion++;
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: unhandled exception baking stage {StageName} for project {ProjectId}", stage.Name, project.Id);
            chatGui.PrintError($"[ReactToMe] Overlay mod builder: unexpected error baking stage \"{stage.Name}\" ({ex.GetType().Name}: {ex.Message}) — check /xllog for details.");
            return false;
        }
    }

    /// <summary>Applies a project: bakes every stage that needs it (per <see cref="OverlayModBuilderStage.NeedsRebake"/>),
    /// then writes/registers the mod via <see cref="OverlayModWriter"/>. Marks the project applied on
    /// success, so the next apply reloads the existing mod instead of re-registering it.</summary>
    public Task<bool> ApplyProjectAsync(OverlayModBuilderProject project) => ApplyProjectAsync(project, forceRebakeAll: false, forceAddMod: false);

    /// <summary>Force-bakes every stage in chain order, ignoring <see cref="OverlayModBuilderStage.NeedsRebake"/>
    /// entirely, then applies. An escape hatch for when a stage's on-disk output needs regenerating for a
    /// reason its own staleness tracking can't see (e.g. this project's earlier bakes landed at a location
    /// a later ReactToMe version no longer looks for) — normal edits should never need this, only
    /// <see cref="ApplyProjectAsync(OverlayModBuilderProject)"/>.</summary>
    public Task<bool> RebakeAllAsync(OverlayModBuilderProject project) => ApplyProjectAsync(project, forceRebakeAll: true, forceAddMod: false);

    /// <summary>Recovers a project whose generated mod was deleted directly in Penumbra: force-rebakes every
    /// stage in chain order (the on-disk state can't be trusted either, since Penumbra losing the mod often
    /// means its folder is gone too) and unconditionally re-registers it via <c>AddMod</c>, ignoring
    /// <see cref="OverlayModBuilderProject.IsApplied"/> — a normal apply would otherwise call
    /// <c>ReloadMod</c> against a mod Penumbra no longer has any record of.</summary>
    public Task<bool> RecreateModAsync(OverlayModBuilderProject project) => ApplyProjectAsync(project, forceRebakeAll: true, forceAddMod: true);

    /// <summary>Permanently deletes a project's mod from Penumbra — its own files/folder included. Called
    /// when the user removes a project that has been applied at least once, so the mod doesn't linger
    /// orphaned in Penumbra's mod list forever. A no-op (returns true) for a project that was never
    /// applied, since there is no mod in Penumbra to delete.</summary>
    public bool DeleteMod(OverlayModBuilderProject project) =>
        !project.IsApplied || penumbraIpc.DeleteGeneratedMod(OverlayModWriter.GetModDirectoryName(project.Id), project.DisplayName);

    /// <summary>Builds a new trigger whose staged-Penumbra-mod reaction has one threshold per this
    /// project's baked stages (ascending, 1-based), targeting the project's own mod and its "Stages" group
    /// — the same source of truth (a stage's file actually existing on disk) that
    /// <see cref="OverlayModWriter.ApplyProject"/> itself uses to decide which stages are baked. The
    /// trigger's fire source (what emote/chat phrase/job skill fires it) is left unconfigured, for the user
    /// to set afterward — this only wires up the Penumbra side.</summary>
    public ReactionTrigger BuildTriggerFromProject(OverlayModBuilderProject project)
    {
        var trigger = new ReactionTrigger
        {
            Name = project.DisplayName,
            PenumbraReactionMode = PenumbraReactionMode.Staged,
        };

        var modFolder = writer.GetModFolderPath(project.Id);
        var modDirectoryName = OverlayModWriter.GetModDirectoryName(project.Id);
        var threshold = 1;

        foreach (var stage in project.Stages)
        {
            if (modFolder == null)
                continue;

            var stageFilePath = OverlayModWriter.ToDiskPath(modFolder, OverlayModWriter.GetStageRelativeFilePath(stage.Id));
            if (!File.Exists(stageFilePath))
                continue;

            trigger.PenumbraStages.Add(new PenumbraStageThreshold
            {
                Threshold = threshold++,
                ModDirectory = modDirectoryName,
                ModName = project.DisplayName,
                OptionGroupName = "Stages",
                OptionName = stage.Name,
            });
        }

        return trigger;
    }

    /// <summary>Bakes every stage up to and including <paramref name="throughIndex"/> that needs it, in
    /// ascending order — never just the one stage at <paramref name="throughIndex"/> in isolation, since a
    /// non-baseline stage's bake reads the immediately preceding stage's own baked output and fails outright
    /// if that doesn't exist yet (e.g. stages were given overlay images out of chain order, or a code-level
    /// file-scheme change left every earlier stage stale). Stages after <paramref name="throughIndex"/> are
    /// left untouched.</summary>
    private async Task BakeStagesThroughAsync(OverlayModBuilderProject project, bool forceRebakeAll, int throughIndex)
    {
        for (var i = 0; i <= throughIndex; i++)
        {
            var stage = project.Stages[i];
            if (!stage.IsBaseline && stage.OverlayImagePath.Length == 0)
                continue;

            int? baseStageBakeVersion = i > 0 ? project.Stages[GetBaseStageIndex(project, i)].BakeVersion : null;
            var needsRebake = forceRebakeAll || stage.NeedsRebake(project.SnapshotVersion, project.ModeVersion, baseStageBakeVersion);
            if (needsRebake)
                await BakeStageAsync(project, stage);
        }
    }

    /// <summary>Bakes a single stage the user just gave/changed an overlay image for, plus every earlier
    /// stage in its chain that also needs baking first — the UI's per-stage "Browse..." action calls this
    /// instead of <see cref="BakeStageAsync"/> directly, so picking an image for a stage later than the
    /// project's most-recently-baked one doesn't fail with "the previous stage needs baking first".</summary>
    public async Task<bool> BakeStageAndChainAsync(OverlayModBuilderProject project, OverlayModBuilderStage stage)
    {
        var index = project.Stages.IndexOf(stage);
        if (index < 0)
        {
            chatGui.PrintError($"[ReactToMe] Overlay mod builder: stage \"{stage.Name}\" is no longer part of this project.");
            return false;
        }

        await BakeStagesThroughAsync(project, forceRebakeAll: false, index);
        return true;
    }

    private async Task<bool> ApplyProjectAsync(OverlayModBuilderProject project, bool forceRebakeAll, bool forceAddMod)
    {
        try
        {
            if (project.TargetActualPath.Length == 0)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: \"{project.DisplayName}\" has no texture chosen to read from.");
                return false;
            }

            if (project.Stages.Count > 0)
                await BakeStagesThroughAsync(project, forceRebakeAll, project.Stages.Count - 1);

            var success = writer.ApplyProject(project.Id, project.DisplayName, project.Stages, alreadyRegistered: project.IsApplied && !forceAddMod);

            if (success)
            {
                project.IsApplied = true;

                if (project.Priority != 0 && !penumbraIpc.SetModPriority(OverlayModWriter.GetModDirectoryName(project.Id), project.DisplayName, project.Priority))
                    chatGui.PrintError($"[ReactToMe] Overlay mod builder: applied \"{project.DisplayName}\" but could not set its priority to {project.Priority} — check /xllog for details.");

                if (project.PenumbraFolder.Length > 0 && !penumbraIpc.SetModPath(OverlayModWriter.GetModDirectoryName(project.Id), $"{project.PenumbraFolder}/{project.DisplayName}", project.DisplayName))
                    chatGui.PrintError($"[ReactToMe] Overlay mod builder: applied \"{project.DisplayName}\" but could not file it under \"{project.PenumbraFolder}\" — check /xllog for details.");
            }
            else
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: failed to apply \"{project.DisplayName}\" — check /xllog for details.");
            }

            return success;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: unhandled exception applying project {ProjectId}", project.Id);
            chatGui.PrintError($"[ReactToMe] Overlay mod builder: unexpected error applying \"{project.DisplayName}\" ({ex.GetType().Name}: {ex.Message}) — check /xllog for details.");
            return false;
        }
    }
}
