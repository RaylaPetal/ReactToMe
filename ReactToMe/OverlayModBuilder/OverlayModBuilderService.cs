using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using ReactToMe.Ipc;
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
    public string? GetStagePreviewPath(OverlayModBuilderProject project, int stageIndex)
    {
        if (project.TargetActualPath.Length == 0)
            return null;

        var modFolder = writer.GetModFolderPath(project.Id);
        return modFolder == null ? null : GetStagePreviewFilePath(modFolder, stageIndex);
    }

    private static string GetStagePreviewFilePath(string modFolder, int stageIndex) =>
        Path.ChangeExtension(OverlayModWriter.ToDiskPath(modFolder, OverlayModWriter.GetStageRelativeFilePath(stageIndex)), ".png");

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
                baseImagePath = GetStagePreviewFilePath(modFolder, stageIndex - 1);
                if (!File.Exists(baseImagePath))
                {
                    chatGui.PrintError($"[ReactToMe] Overlay mod builder: stage \"{stage.Name}\" needs the previous stage baked first — missing {baseImagePath}.");
                    return false;
                }
            }

            var relativeFilePath = OverlayModWriter.GetStageRelativeFilePath(stageIndex);
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
            stage.LastBakedFileSchemeVersion = OverlayModWriter.StageFileSchemeVersion;
            stage.LastBakedPredecessorVersion = stageIndex > 0 ? project.Stages[stageIndex - 1].BakeVersion : -1;
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

    private async Task<bool> ApplyProjectAsync(OverlayModBuilderProject project, bool forceRebakeAll, bool forceAddMod)
    {
        try
        {
            if (project.TargetActualPath.Length == 0)
            {
                chatGui.PrintError($"[ReactToMe] Overlay mod builder: \"{project.DisplayName}\" has no texture chosen to read from.");
                return false;
            }

            for (var i = 0; i < project.Stages.Count; i++)
            {
                var stage = project.Stages[i];
                if (!stage.IsBaseline && stage.OverlayImagePath.Length == 0)
                    continue;

                int? predecessorBakeVersion = i > 0 ? project.Stages[i - 1].BakeVersion : null;
                var needsRebake = forceRebakeAll || stage.NeedsRebake(project.SnapshotVersion, predecessorBakeVersion);
                if (needsRebake)
                    await BakeStageAsync(project, stage);
            }

            var stageNames = project.Stages.Select(s => s.Name).ToList();
            var success = writer.ApplyProject(project.Id, project.DisplayName, stageNames, alreadyRegistered: project.IsApplied && !forceAddMod);

            if (success)
            {
                project.IsApplied = true;

                if (project.Priority != 0 && !penumbraIpc.SetModPriority(OverlayModWriter.GetModDirectoryName(project.Id), project.DisplayName, project.Priority))
                    chatGui.PrintError($"[ReactToMe] Overlay mod builder: applied \"{project.DisplayName}\" but could not set its priority to {project.Priority} — check /xllog for details.");
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
