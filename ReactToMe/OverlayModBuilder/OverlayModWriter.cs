using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;
using ReactToMe.Ipc;

namespace ReactToMe.OverlayModBuilder;

/// <summary>
/// Writes an overlay-mod-builder project's generated Penumbra mod folder and registers/reloads it with
/// Penumbra. The mod-file shape (meta.json with one single-select group, options embedded inline) was
/// confirmed live against the user's installed Penumbra version by the spike in <see cref="SpikeTest"/>
/// before this writer was built.
/// </summary>
public sealed class OverlayModWriter
{
    private readonly PenumbraIpc penumbraIpc;
    private readonly IPluginLog log;

    public OverlayModWriter(PenumbraIpc penumbraIpc, IPluginLog log)
    {
        this.penumbraIpc = penumbraIpc;
        this.log = log;
    }

    /// <summary>Stable per-project mod directory name, derived from the project's id rather than its
    /// display name, so renaming a project never orphans its already-registered mod.</summary>
    public static string GetModDirectoryName(Guid projectId) => $"ReactToMeOverlay-{projectId:N}";

    /// <summary>The one and only game path this tool's generated mods ever redirect. Fixed by explicit
    /// product decision — this tool exists solely to build staged tattoo/skin overlays on top of the Bibo
    /// body base texture, never an arbitrary target — so there is no per-project target-path concept at all:
    /// a project only ever picks which live-resolving texture to read pixel data <em>from</em> (see
    /// <see cref="OverlayModBuilderProject.TargetActualPath"/>); what the generated mod redirects is always
    /// this constant, no exceptions.</summary>
    public const string RedirectGamePath = "chara/bibo_mid_base.tex";

    /// <summary>Bump whenever <see cref="GetStageRelativeFilePath"/>'s scheme changes. Compared against each
    /// stage's own <see cref="OverlayModBuilderStage.LastBakedFileSchemeVersion"/> so a code-level path
    /// change — which a stage's own overlay image and the project's snapshot version can never reflect —
    /// always forces every stage past its own staleness tracking and gets re-baked at its new location,
    /// rather than silently leaving already-"successfully baked" stages pointing nowhere (this has already
    /// happened twice: once when stage files moved from the mod root into a mirrored directory, and once
    /// when that mirroring itself was replaced by this fixed scheme).</summary>
    public const int StageFileSchemeVersion = 2;

    /// <summary>Stable per-stage relative file path within a project's mod folder. Deliberately independent
    /// of the target game path's own content: a target picked from <c>GetTextureOverlayCandidates</c> can
    /// be a real virtual game path (e.g. "chara/human/.../c1801b0001_d.tex") or a raw absolute override path
    /// from another mod that redirects via "whatever's currently resolving" (e.g. a Windows path like
    /// "Z:\...\Skin Overlay Kaede\chara\kaede overlay.tex") — parsing the target's own structure previously
    /// broke on the latter, since it uses backslashes and no forward slash was ever found, and the resulting
    /// "relative" path was actually still absolute, which made <see cref="ToDiskPath"/>'s Path.Combine
    /// discard the mod folder entirely and silently write into the OTHER mod's own directory instead. A real
    /// working mod's own layout was checked directly early on: only the top-level folder needs to be named
    /// "chara" (not nested under any other folder) — the exact file name never mattered. This always emits
    /// that fixed, safe shape, uses forward slashes (game-path convention); callers writing to disk should
    /// normalize via <see cref="ToDiskPath"/>.</summary>
    public static string GetStageRelativeFilePath(int stageIndex) => $"chara/reacttome_overlay_stage{stageIndex}.tex";

    /// <summary>Converts a forward-slash relative path (game-path convention, also used for the mod's
    /// internal <c>Files</c> mapping) into a real path under a folder, using the local platform's directory
    /// separator.</summary>
    public static string ToDiskPath(string folder, string relativeForwardSlashPath) =>
        Path.Combine(folder, relativeForwardSlashPath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>The folder a project's stages should be baked into and where its mod files live, or null if
    /// Penumbra's own mod directory couldn't be resolved.</summary>
    public string? GetModFolderPath(Guid projectId)
    {
        var penumbraModRoot = penumbraIpc.GetModDirectory();
        return string.IsNullOrEmpty(penumbraModRoot) ? null : Path.Combine(penumbraModRoot, GetModDirectoryName(projectId));
    }

    /// <summary>Writes (or overwrites) a project's <c>meta.json</c> — one single-select group with one
    /// option per stage that has a successfully baked texture already sitting in the mod folder (an
    /// unbaked or failed stage is simply left out, never applied as a broken option) — and registers or
    /// reloads it with Penumbra depending on whether this project has been applied before. Returns whether
    /// the whole operation succeeded.</summary>
    public bool ApplyProject(Guid projectId, string displayName, IReadOnlyList<string> stageNames, bool alreadyRegistered)
    {
        var modFolder = GetModFolderPath(projectId);
        if (modFolder == null)
        {
            log.Warning("Overlay mod builder: could not resolve Penumbra's mod directory while applying project {ProjectId}", projectId);
            return false;
        }

        Directory.CreateDirectory(modFolder);

        var options = new List<OverlayOption>();
        for (var i = 0; i < stageNames.Count; i++)
        {
            var relativeFilePath = GetStageRelativeFilePath(i);
            var stageFilePath = ToDiskPath(modFolder, relativeFilePath);
            if (!File.Exists(stageFilePath))
            {
                log.Information("Overlay mod builder: stage \"{StageName}\" has no baked file at {StageFilePath} — leaving it out of this apply", stageNames[i], stageFilePath);
                continue;
            }

            options.Add(new OverlayOption
            {
                Name = stageNames[i],
                Files = { [RedirectGamePath] = relativeFilePath },
            });
        }

        if (options.Count == 0)
        {
            log.Warning("Overlay mod builder: project {ProjectId} has no successfully baked stages to apply", projectId);
            return false;
        }

        var meta = new OverlayModMeta
        {
            Name = displayName,
            Author = "ReactToMe",
            Description = "Generated by ReactToMe's Overlay Mod Builder — edit stages from there, not by hand.",
            Groups = [new OverlayGroup { Name = "Stages", Type = "Single", Options = options }],
        };

        try
        {
            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(modFolder, "meta.json"), metaJson);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: failed to write meta.json for project {ProjectId}", projectId);
            return false;
        }

        var modDirectoryName = GetModDirectoryName(projectId);
        return alreadyRegistered
            ? penumbraIpc.ReloadGeneratedMod(modDirectoryName, displayName)
            : penumbraIpc.AddGeneratedMod(modDirectoryName);
    }

    private sealed class OverlayModMeta
    {
        public int FileVersion { get; set; } = 4;
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "0.0.1";
        public string Website { get; set; } = string.Empty;
        public List<string> ModTags { get; set; } = [];
        public OverlayModDataContainer DefaultData { get; set; } = new();
        public List<OverlayGroup> Groups { get; set; } = [];
    }

    private sealed class OverlayModDataContainer
    {
        public Dictionary<string, string> Files { get; set; } = new();
        public Dictionary<string, string> FileSwaps { get; set; } = new();
        public List<object> Manipulations { get; set; } = [];
    }

    private sealed class OverlayGroup
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Type { get; set; } = "Single";
        public int DefaultSettings { get; set; }
        public List<OverlayOption> Options { get; set; } = [];
    }

    private sealed class OverlayOption
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public Dictionary<string, string> Files { get; set; } = new();
        public Dictionary<string, string> FileSwaps { get; set; } = new();
        public List<object> Manipulations { get; set; } = [];
    }
}
