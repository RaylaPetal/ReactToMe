using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ECommons.Interop;
using Penumbra.Api.Enums;
using ReactToMe.Effects;
using ReactToMe.Ipc;
using ReactToMe.JobSkills;
using ReactToMe.OverlayModBuilder;
using ReactToMe.Triggers;

namespace ReactToMe.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private readonly Dictionary<string, string> searchFilters = new();

    private IReadOnlyDictionary<uint, string> emotes = new Dictionary<uint, string>();
    private IReadOnlyDictionary<uint, string> gestureEmotes = new Dictionary<uint, string>();
    private IReadOnlyDictionary<uint, string> jobs = new Dictionary<uint, string>();
    private readonly Dictionary<uint, IReadOnlyDictionary<uint, string>> jobSkillsByJobCache = new();
    private IReadOnlyDictionary<Guid, string> designs = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, MoodleInfo> moodles = new Dictionary<Guid, MoodleInfo>();
    private IReadOnlyDictionary<string, string> penumbraMods = new Dictionary<string, string>();
    private readonly Dictionary<string, IReadOnlyDictionary<string, (string[] Options, GroupType Type)>> penumbraModSettingsCache = new();
    private IReadOnlyDictionary<string, string> textureOverlayCandidates = new Dictionary<string, string>();
    private bool listsLoaded;
    private readonly HashSet<Guid> busyOverlayProjectIds = new();
    private Guid? pendingRemoveOverlayProjectId;
    private readonly Dictionary<string, (DateTime LastWriteUtc, IDalamudTextureWrap Wrap)> overlayPreviewWraps = new();

    private Guid? selectedTriggerId;
    private bool forceSelectTriggersTab;
    private Guid? pendingRemoveTriggerId;
    private string triggerListFilter = string.Empty;

    private Guid? selectedOverlayProjectId;

    public ConfigWindow(Plugin plugin) : base("ReactToMe Configuration###ReactToMe config window")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(860, 560);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose()
    {
        foreach (var (_, wrap) in overlayPreviewWraps.Values)
            wrap.Dispose();
        overlayPreviewWraps.Clear();
    }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    private void RefreshLists()
    {
        emotes = plugin.EmoteCatalog.GetEmotes();
        gestureEmotes = plugin.EmoteCatalog.GetGestureEmotes();
        jobs = plugin.JobSkillCatalog.GetJobs();
        jobSkillsByJobCache.Clear();
        designs = plugin.GlamourerIpc.GetDesigns();
        moodles = plugin.MoodlesIpc.GetMoodles();
        penumbraMods = plugin.PenumbraIpc.GetMods();
        penumbraModSettingsCache.Clear();
        textureOverlayCandidates = plugin.PenumbraIpc.GetTextureOverlayCandidates();
        listsLoaded = true;
    }

    private IReadOnlyDictionary<string, (string[] Options, GroupType Type)> GetPenumbraModSettings(string modDirectory, string modName)
    {
        if (!penumbraModSettingsCache.TryGetValue(modDirectory, out var settings))
        {
            settings = plugin.PenumbraIpc.GetModSettings(modDirectory, modName);
            penumbraModSettingsCache[modDirectory] = settings;
        }

        return settings;
    }

    private IReadOnlyDictionary<uint, string> GetJobSkills(uint classJobId)
    {
        if (!jobSkillsByJobCache.TryGetValue(classJobId, out var actions))
        {
            actions = plugin.JobSkillCatalog.GetActionsForJob(classJobId);
            jobSkillsByJobCache[classJobId] = actions;
        }

        return actions;
    }

    /// <summary>The trigger's configured display name if set, else a label generated from its source
    /// type and configuration — used for the trigger list and its filter.</summary>
    private string GetTriggerLabel(ReactionTrigger trigger) => TriggerLabeler.GetLabel(trigger, emotes, plugin.JobSkillCatalog);

    public override void Draw()
    {
        if (!listsLoaded)
            RefreshLists();

        if (ImGui.BeginTabBar("###configTabs"))
        {
            if (ImGui.BeginTabItem("Active Effects"))
            {
                DrawActiveEffectsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Triggers", forceSelectTriggersTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                forceSelectTriggersTab = false;
                DrawTriggersTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Overlay Mod Builder"))
            {
                DrawOverlayModBuilderTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawActiveEffectsTab()
    {
        ImGui.Spacing();

        var activeEffects = plugin.EffectRegistry.ActiveEffects;
        if (activeEffects.Count == 0)
        {
            ImGui.TextDisabled("None.");
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var effect in activeEffects)
        {
            var trigger = configuration.Triggers.FirstOrDefault(t => t.Id == effect.TriggerId);
            var label = trigger != null ? GetTriggerLabel(trigger) : "(unknown trigger)";
            var status = effect.ExpiresAtUtc is { } expiresAt
                ? $"reverts in {FormatRemaining(expiresAt - now)}"
                : "no expiration";
            ImGui.TextUnformatted($"{label} — {status}");
        }

        ImGui.Spacing();
        if (ImGui.Button("Force revert all"))
            plugin.EffectRegistry.RevertAll();
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return "0s";

        var days = remaining.Days;
        var hours = remaining.Hours;
        var minutes = remaining.Minutes;
        var seconds = remaining.Seconds;

        var parts = new List<string>();
        if (days > 0)
            parts.Add($"{days}d");
        if (days > 0 || hours > 0)
            parts.Add($"{hours}h");
        if (days > 0 || hours > 0 || minutes > 0)
            parts.Add($"{minutes}m");
        parts.Add($"{seconds}s");

        return string.Join(" ", parts);
    }

    /// <summary>Individual channel types offered as their own checkbox, plus one entry per grouped channel
    /// family (all 8 Linkshells, all 8 Cross-world Linkshells) toggled as a unit — a user wants "watch my
    /// linkshells" as one concept, not to pick individually among LS1-8, so exposing all 16 numbered
    /// channels as separate checkboxes would only add clutter without adding real choice.</summary>
    private static readonly (string Label, XivChatType[] Channels)[] ChatChannelOptions =
    [
        ("Say", [XivChatType.Say]),
        ("Yell", [XivChatType.Yell]),
        ("Shout", [XivChatType.Shout]),
        ("Tell", [XivChatType.TellIncoming, XivChatType.TellOutgoing]),
        ("Party", [XivChatType.Party]),
        ("Alliance", [XivChatType.Alliance]),
        ("Free Company", [XivChatType.FreeCompany]),
        ("Linkshells", [XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4, XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8]),
        ("Cross-world Linkshells", [XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8]),
        ("Novice Network", [XivChatType.NoviceNetwork]),
    ];

    private void DrawSettingsTab()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("General");

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        var revertOnRelog = configuration.RevertOnRelog;
        if (ImGui.Checkbox("Revert active effect on logout", ref revertOnRelog))
        {
            configuration.RevertOnRelog = revertOnRelog;
            configuration.Save();
        }

        var reactionsEnabled = configuration.ReactionsEnabled;
        if (ImGui.Checkbox("Reactions enabled", ref reactionsEnabled))
        {
            configuration.ReactionsEnabled = reactionsEnabled;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Master switch for every trigger at once. Turning this off stops all trigger matching — a fast way to go quiet for a call or stream — without changing any individual trigger's own enabled state; turning it back on restores matching exactly as each trigger was already configured. Already-active effects from before this was turned off are unaffected.");

        ImGui.Separator();
        ImGui.TextDisabled("Watched Chat Channels");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Which chat channels are watched for chat-phrase triggers. Say/Yell/Shout/Tell/Party/Alliance/Free Company are watched by default, matching this plugin's prior fixed behavior.");

        const int columns = 3;
        const float columnWidth = 170f;
        for (var i = 0; i < ChatChannelOptions.Length; i++)
        {
            var (label, channels) = ChatChannelOptions[i];

            // Three per row keeps the section compact without risking horizontal overflow from packing
            // all ten options onto one line.
            var column = i % columns;
            if (column != 0)
                ImGui.SameLine(column * columnWidth);

            var allWatched = channels.All(configuration.WatchedChatChannels.Contains);
            if (ImGui.Checkbox(label, ref allWatched))
            {
                if (allWatched)
                    foreach (var channel in channels)
                    {
                        if (!configuration.WatchedChatChannels.Contains(channel))
                            configuration.WatchedChatChannels.Add(channel);
                    }
                else
                    configuration.WatchedChatChannels.RemoveAll(channels.Contains);

                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("New Trigger Defaults");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Seed values a newly-created trigger starts with. Changing these never alters any already-existing trigger's own configured values.");

        var defaultDurationMinutes = (float)configuration.DefaultTriggerDuration.TotalMinutes;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Default duration (minutes)", ref defaultDurationMinutes))
        {
            configuration.DefaultTriggerDuration = TimeSpan.FromMinutes(Math.Max(0, defaultDurationMinutes));
            configuration.Save();
        }

        var defaultChatCooldown = configuration.DefaultChatCooldownSeconds;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Default chat cooldown (seconds)", ref defaultChatCooldown))
        {
            configuration.DefaultChatCooldownSeconds = Math.Max(0, defaultChatCooldown);
            configuration.Save();
        }
    }

    /// <summary>Authoring workflow for a permanent, multi-stage Penumbra mod baked from imported overlay
    /// images — entirely independent of trigger configuration. Once applied, a project's generated mod is
    /// picked by a trigger's existing staged-Penumbra-mod reaction exactly like any other pre-built mod;
    /// this tab never touches trigger-firing behavior.</summary>
    private void DrawOverlayModBuilderTab()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted($"Overlay Mod Builder Projects ({configuration.OverlayModBuilderProjects.Count})");

        ImGui.SameLine();
        if (ImGui.Button("Refresh lists"))
            RefreshLists();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Re-fetches the textures currently resolving on your character — use this after changing gear/mods.");

        ImGui.SameLine();
        if (ImGui.Button("Add Project"))
        {
            var newProject = new OverlayModBuilderProject();
            configuration.OverlayModBuilderProjects.Add(newProject);
            selectedOverlayProjectId = newProject.Id;
            configuration.Save();
        }

        ImGui.Separator();

        var listWidth = MathF.Max(180f, ImGui.GetContentRegionAvail().X * 0.3f);

        ImGui.BeginChild("overlayProjectList", new Vector2(listWidth, 0), true);
        DrawOverlayProjectList();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("overlayProjectDetail", new Vector2(0, 0), true);
        var selected = configuration.OverlayModBuilderProjects.FirstOrDefault(p => p.Id == selectedOverlayProjectId);
        if (selected == null)
            ImGui.TextDisabled("Select a project on the left, or add one.");
        else
            DrawOverlayProjectDetail(selected);
        ImGui.EndChild();

        // Opened outside every stage's PushID scope so the popup's identity never gets mangled by whichever
        // stage's thumbnail was clicked.
        if (overlayPreviewPopupShouldOpen)
        {
            ImGui.OpenPopup("Overlay Preview");
            overlayPreviewPopupShouldOpen = false;
        }

        DrawOverlayPreviewPopup();
    }

    private string? overlayPreviewToEnlarge;
    private bool overlayPreviewPopupShouldOpen;

    private void DrawOverlayPreviewPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(640, 640), ImGuiCond.FirstUseEver);
        var open = true;
        if (!ImGui.BeginPopupModal("Overlay Preview", ref open))
            return;

        if (overlayPreviewToEnlarge != null && File.Exists(overlayPreviewToEnlarge) && TryGetOverlayPreviewWrap(overlayPreviewToEnlarge, out var wrap))
        {
            var avail = ImGui.GetContentRegionAvail();
            var scale = MathF.Min(avail.X / wrap.Width, avail.Y / wrap.Height);
            ImGui.Image(wrap.Handle, new Vector2(wrap.Width * scale, wrap.Height * scale));
        }
        else
        {
            ImGui.TextDisabled("(preview no longer available)");
        }

        if (!open)
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    /// <summary>Runs a long-running overlay-mod-builder operation in the background, marking the project
    /// busy for its duration so its buttons can disable themselves instead of the operation reporting its
    /// own progress to chat — the UI itself (snapshot/stage state, "Applied to Penumbra") already reflects
    /// the result once it's done.</summary>
    private void RunOverlayBusyTask(Guid projectId, Func<Task> action)
    {
        busyOverlayProjectIds.Add(projectId);
        _ = Task.Run(async () =>
        {
            try
            {
                await action();
            }
            finally
            {
                busyOverlayProjectIds.Remove(projectId);
                configuration.Save();
            }
        });
    }

    private void DrawOverlayProjectList()
    {
        foreach (var project in configuration.OverlayModBuilderProjects)
        {
            var label = project.IsApplied ? project.DisplayName : $"{project.DisplayName} (not applied)";
            ImGui.PushID(project.Id.GetHashCode());
            if (ImGui.Selectable(label, project.Id == selectedOverlayProjectId))
                selectedOverlayProjectId = project.Id;
            ImGui.PopID();
        }
    }

    private void DrawOverlayProjectDetail(OverlayModBuilderProject project)
    {
        ImGui.PushID(project.Id.GetHashCode());

        if (project.IsApplied && pendingRemoveOverlayProjectId == project.Id)
        {
            if (ImGui.Button("Confirm Remove (deletes Penumbra mod too)"))
            {
                plugin.OverlayModBuilderService.DeleteMod(project);
                configuration.OverlayModBuilderProjects.Remove(project);
                selectedOverlayProjectId = null;
                pendingRemoveOverlayProjectId = null;
                configuration.Save();
                ImGui.PopID();
                return;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This permanently deletes the mod from Penumbra's own mod list, in addition to removing the project from ReactToMe. This can't be undone.");

            ImGui.SameLine();
            if (ImGui.Button("Cancel##cancelRemoveOverlayProject"))
                pendingRemoveOverlayProjectId = null;
        }
        else
        {
            if (ImGui.Button("Remove Project"))
            {
                if (project.IsApplied)
                {
                    pendingRemoveOverlayProjectId = project.Id;
                }
                else
                {
                    configuration.OverlayModBuilderProjects.Remove(project);
                    selectedOverlayProjectId = null;
                    configuration.Save();
                    ImGui.PopID();
                    return;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(project.IsApplied
                    ? "Removes this project from ReactToMe and deletes its mod from Penumbra — requires confirming, since that deletion can't be undone."
                    : "Removes this project from ReactToMe. It was never applied, so there's no Penumbra mod to clean up.");
        }

        var name = project.DisplayName;
        if (ImGui.InputText("Display name", ref name, 64))
        {
            project.DisplayName = name;
            configuration.Save();
        }

        ImGui.TextDisabled(project.IsApplied ? "Applied to Penumbra" : "Not applied yet");

        ImGui.Separator();
        ImGui.TextDisabled("Base texture (read from)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Every texture currently resolving on your character — search for whatever mod/texture you want to use as the base to build tattoo/skin overlays on top of (e.g. type \"kaede\" or \"body\"). Not filtered by naming convention, since body mods vary too much to auto-detect reliably. The generated mod always redirects \"" + OverlayModWriter.RedirectGamePath + "\" once applied, regardless of what you pick here.");

        var displayItems = textureOverlayCandidates.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Length > 0 ? $"{kv.Key} ({Path.GetFileName(kv.Value)})" : kv.Key);

        if (DrawSearchablePicker("Target", $"{project.Id}-overlay-target", displayItems, project.SourceGamePath, string.Empty, out var newTarget))
        {
            project.SourceGamePath = newTarget;
            project.TargetActualPath = textureOverlayCandidates.GetValueOrDefault(newTarget, string.Empty);
            configuration.Save();
        }

        if (project.TargetActualPath.Length == 0)
        {
            ImGui.PopID();
            return;
        }

        ImGui.Spacing();
        var isBusy = busyOverlayProjectIds.Contains(project.Id);
        var hasSnapshot = project.PristineSnapshotPath.Length > 0 && File.Exists(project.PristineSnapshotPath);
        ImGui.BeginDisabled(isBusy);
        if (ImGui.Button(hasSnapshot ? "Recapture Snapshot" : "Capture Snapshot"))
            RunOverlayBusyTask(project.Id, () => plugin.OverlayModBuilderService.CaptureSnapshotAsync(project));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Reads the target texture as it's currently resolving and stores it as this project's base — every stage bakes against this same snapshot, never the live texture at bake time. Recapture explicitly if you change your skin/body mod and want stages to reflect it.");

        ImGui.SameLine();
        ImGui.TextDisabled(isBusy ? "Working..." : hasSnapshot ? "Snapshot captured" : "No snapshot yet");

        if (!hasSnapshot)
        {
            ImGui.PopID();
            return;
        }

        ImGui.Separator();
        ImGui.TextDisabled("Stages (each becomes one option in the generated mod)");

        var removeIndex = -1;
        for (var s = 0; s < project.Stages.Count; s++)
        {
            var stage = project.Stages[s];
            ImGui.PushID(s);

            if (stage.IsBaseline)
            {
                ImGui.TextUnformatted("Stage 0 — Baseline (from snapshot, no overlay)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Auto-generated from this project's pristine snapshot every time it's captured or recaptured. Not removable — every later stage's chain starts from it.");
            }
            else
            {
                var stageName = stage.Name;
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputText("Name", ref stageName, 64))
                {
                    stage.Name = stageName;
                    configuration.Save();
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(stage.OverlayImagePath.Length > 0 ? Path.GetFileName(stage.OverlayImagePath) : "(no image)");

                ImGui.SameLine();
                ImGui.BeginDisabled(isBusy);
                if (ImGui.Button("Browse..."))
                {
                    var capturedProject = project;
                    var capturedStage = stage;
                    OpenFileDialog.SelectFile(
                        ofn =>
                        {
                            var imported = ImportOverlayImage(capturedProject.Id, capturedStage.Id, ofn.file);
                            if (imported == null)
                                return;

                            capturedStage.OverlayImagePath = imported;
                            configuration.Save();
                            RunOverlayBusyTask(capturedProject.Id, () => plugin.OverlayModBuilderService.BakeStageAndChainAsync(capturedProject, capturedStage));
                        },
                        title: "Select overlay image",
                        fileTypes: [("Images", new[] { "png" })]);
                }

                ImGui.SameLine();
                if (ImGui.Button("Remove##removeOverlayStage"))
                    removeIndex = s;
                ImGui.EndDisabled();
            }

            var predecessorBakeVersion = s > 0 ? project.Stages[s - 1].BakeVersion : (int?)null;
            if (stage.NeedsRebake(project.SnapshotVersion, predecessorBakeVersion))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), "(needs rebake — will bake on next Apply)");
            }

            DrawOverlayStagePreview(project, stage);

            ImGui.Separator();
            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            plugin.OverlayModBuilderService.DeleteStageFiles(project, project.Stages[removeIndex]);
            project.Stages.RemoveAt(removeIndex);
            configuration.Save();
        }

        if (ImGui.Button("Add Stage"))
        {
            // Naming matches this stage's own index: Stage 0 is always the baseline, so the first
            // user-added stage (landing at index 1) is "Stage 1", not "Stage 2".
            project.Stages.Add(new OverlayModBuilderStage { Name = $"Stage {project.Stages.Count}" });
            configuration.Save();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(isBusy);
        if (ImGui.Button("Apply") && project.Stages.Count > 0)
            RunOverlayBusyTask(project.Id, () => plugin.OverlayModBuilderService.ApplyProjectAsync(project));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Bakes every stage that needs it, then writes/registers this project's mod with Penumbra. Re-applying updates the same mod in place rather than creating a duplicate.");

        ImGui.SameLine();
        ImGui.BeginDisabled(isBusy);
        if (ImGui.Button("Rebake All") && project.Stages.Count > 0)
            RunOverlayBusyTask(project.Id, () => plugin.OverlayModBuilderService.RebakeAllAsync(project));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Force-bakes every stage regardless of whether it looks up to date, then applies. Use this if a stage's preview or in-game result seems stale or wrong even though nothing looks like it needs rebaking.");

        ImGui.SameLine();
        ImGui.BeginDisabled(isBusy);
        if (ImGui.Button("Recreate Mod") && project.Stages.Count > 0)
            RunOverlayBusyTask(project.Id, () => plugin.OverlayModBuilderService.RecreateModAsync(project));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Use this if you deleted this project's mod directly in Penumbra. Force-rebakes every stage and re-registers the mod from scratch, instead of trying (and failing) to reload a mod Penumbra no longer has any record of.");

        ImGui.SameLine();
        if (ImGui.Button("Create Trigger"))
        {
            // The project's mod may have been registered with Penumbra after these lists were last
            // fetched (e.g. applied earlier in this same session) — refresh so the Triggers tab's own
            // mod/option pickers actually recognize it instead of showing it as unknown.
            RefreshLists();
            var trigger = plugin.OverlayModBuilderService.BuildTriggerFromProject(project);
            configuration.Triggers.Add(trigger);
            selectedTriggerId = trigger.Id;
            forceSelectTriggersTab = true;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Creates a new trigger with one threshold per baked stage already pointing at this project's mod — you'll still need to set what fires it (emote/chat phrase/job skill) in the Triggers tab, which this switches to.");

        if (isBusy)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Working...");
        }

        ImGui.Spacing();
        var priority = project.Priority;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Priority", ref priority))
        {
            project.Priority = priority;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This mod's priority against other enabled mods in Penumbra — decides which one wins when more than one redirects the same file (e.g. another mod also touching " + OverlayModWriter.RedirectGamePath + "). 0 is Penumbra's own default; higher wins. Applied every time this project is applied or its mod recreated.");

        var penumbraFolder = project.PenumbraFolder;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("Penumbra folder", ref penumbraFolder, 128))
        {
            project.PenumbraFolder = penumbraFolder;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Files this mod under a folder in Penumbra's own mod list (e.g. \"Body\"), the same as typing a path in Penumbra's UI. Leave empty to leave the mod wherever Penumbra already has it. Applied every time this project is applied or its mod recreated.");

        ImGui.PopID();
    }

    /// <summary>Copies the user's picked file into ReactToMe's own plugin data directory, so a project
    /// never depends on the original external file staying where it was. Returns the managed copy's path,
    /// or null if the source file doesn't exist.</summary>
    private static string? ImportOverlayImage(Guid projectId, Guid stageId, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        var directory = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "OverlayModBuilderSources");
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, $"{projectId:N}-stage-{stageId:N}{Path.GetExtension(sourcePath)}");
        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    /// <summary>Shows a small thumbnail of what was actually composited for this stage's last bake attempt,
    /// so a compositing problem (wrong image, bad UV/resolution match) can be told apart from an
    /// application problem (the composite looks right but doesn't show up on the character).</summary>
    private void DrawOverlayStagePreview(OverlayModBuilderProject project, OverlayModBuilderStage stage)
    {
        var previewPath = plugin.OverlayModBuilderService.GetStagePreviewPath(project, stage);
        if (previewPath == null || !File.Exists(previewPath))
        {
            ImGui.TextDisabled("(no preview yet — bake this stage first)");
            return;
        }

        if (!TryGetOverlayPreviewWrap(previewPath, out var wrap))
        {
            ImGui.TextDisabled("(preview file exists but failed to load as an image)");
            return;
        }

        const float maxDimension = 96f;
        var scale = MathF.Min(1f, maxDimension / MathF.Max(wrap.Width, wrap.Height));
        ImGui.TextDisabled("Last composited preview (click to enlarge):");
        ImGui.Image(wrap.Handle, new Vector2(wrap.Width * scale, wrap.Height * scale));
        if (ImGui.IsItemClicked())
        {
            overlayPreviewToEnlarge = previewPath;
            overlayPreviewPopupShouldOpen = true;
        }
    }

    /// <summary>Decodes a preview PNG ourselves and uploads it as a raw texture, cached and keyed by file
    /// path + last-write time, instead of Dalamud's own file-backed texture cache
    /// (<c>ITextureProvider.GetFromFile</c>): that cache is keyed purely by path and doesn't know this
    /// specific file gets overwritten in place on every rebake, so it can get stuck showing a stale or
    /// failed load from before the file existed. Re-decodes only when the file's own last-write time moves
    /// past what's cached.</summary>
    private bool TryGetOverlayPreviewWrap(string previewPath, out IDalamudTextureWrap wrap)
    {
        var lastWriteUtc = File.GetLastWriteTimeUtc(previewPath);
        if (overlayPreviewWraps.TryGetValue(previewPath, out var cached) && cached.LastWriteUtc == lastWriteUtc)
        {
            wrap = cached.Wrap;
            return true;
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(previewPath);
            var pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels);

            var newWrap = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(image.Width, image.Height), pixels, "ReactToMe overlay preview");

            if (overlayPreviewWraps.TryGetValue(previewPath, out var old))
                old.Wrap.Dispose();

            overlayPreviewWraps[previewPath] = (lastWriteUtc, newWrap);
            wrap = newWrap;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Overlay mod builder: failed to decode preview {PreviewPath}", previewPath);
            wrap = null!;
            return false;
        }
    }

    private void DrawTriggersTab()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted($"Triggers ({configuration.Triggers.Count})");

        ImGui.SameLine();
        if (ImGui.Button("Refresh lists"))
            RefreshLists();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Re-fetches emotes, job skills, and your current Glamourer designs / Moodles / Penumbra mods — use this after creating a new one in-game.");

        ImGui.SameLine();
        if (ImGui.Button("Add Trigger"))
        {
            var newTrigger = new ReactionTrigger
            {
                Duration = configuration.DefaultTriggerDuration,
                ChatCooldownSeconds = configuration.DefaultChatCooldownSeconds,
            };
            configuration.Triggers.Add(newTrigger);
            selectedTriggerId = newTrigger.Id;
            configuration.Save();
        }

        ImGui.Separator();

        var listWidth = MathF.Max(180f, ImGui.GetContentRegionAvail().X * 0.3f);

        ImGui.BeginChild("triggerList", new Vector2(listWidth, 0), true);
        DrawTriggerList();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("triggerDetail", new Vector2(0, 0), true);
        var selected = configuration.Triggers.FirstOrDefault(t => t.Id == selectedTriggerId);
        if (selected == null)
            ImGui.TextDisabled("Select a trigger on the left, or add one.");
        else
            DrawTriggerDetail(selected);
        ImGui.EndChild();
    }

    private void DrawTriggerList()
    {
        var filter = triggerListFilter;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##triggerFilter", "Filter...", ref filter, 128))
            triggerListFilter = filter;

        ImGui.Separator();

        foreach (var trigger in configuration.Triggers)
        {
            var label = GetTriggerLabel(trigger);
            if (filter.Length > 0 && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var displayLabel = trigger.IsEnabled ? label : $"{label} (disabled)";
            ImGui.PushID(trigger.Id.GetHashCode());
            if (ImGui.Selectable(displayLabel, trigger.Id == selectedTriggerId))
                selectedTriggerId = trigger.Id;
            ImGui.PopID();
        }
    }

    private void DrawTriggerDetail(ReactionTrigger trigger)
    {
        ImGui.PushID(trigger.Id.GetHashCode());

        var enabled = trigger.IsEnabled;
        if (ImGui.Checkbox("Enabled##enabled", ref enabled))
        {
            trigger.IsEnabled = enabled;
            configuration.Save();
        }

        ImGui.SameLine();
        if (pendingRemoveTriggerId == trigger.Id)
        {
            if (ImGui.Button("Confirm Remove"))
            {
                configuration.Triggers.Remove(trigger);
                selectedTriggerId = null;
                pendingRemoveTriggerId = null;
                configuration.Save();
                ImGui.PopID();
                return;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel##cancelRemoveTrigger"))
                pendingRemoveTriggerId = null;
        }
        else
        {
            if (ImGui.Button("Remove Trigger"))
                pendingRemoveTriggerId = trigger.Id;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Deleting a trigger can't be undone — requires confirming.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Test Fire"))
            plugin.TestFireTrigger(trigger);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Applies this trigger's configured reactions immediately, exactly as a real matching emote/chat phrase/job skill would — without waiting for that to actually happen. Subject to the same chat/gesture cooldown a real repeat fire would be.");

        var name = trigger.Name;
        if (ImGui.InputText("Display name", ref name, 64))
        {
            trigger.Name = name;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Shown in the list on the left. Leave blank to use a label generated from this trigger's source.");

        DrawSectionHeader("Source");
        DrawSourceSection(trigger);

        DrawSectionHeader("Reactions");
        DrawReactionsSection(trigger);

        DrawSectionHeader("Timing & Revert");
        DrawTimingSection(trigger);

        ImGui.PopID();
    }

    private static void DrawSectionHeader(string label)
    {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.75f, 0.6f, 0.85f, 1f), label);
        ImGui.Separator();
    }

    private void DrawSourceSection(ReactionTrigger trigger)
    {
        var sourceTypePreview = trigger.TriggerSourceType switch
        {
            TriggerSourceType.Emote => "Emote",
            TriggerSourceType.ChatPhrase => "Chat phrase",
            TriggerSourceType.JobSkill => "Job skill",
            _ => trigger.TriggerSourceType.ToString(),
        };
        if (ImGui.BeginCombo("Trigger type", sourceTypePreview))
        {
            if (ImGui.Selectable("Emote", trigger.TriggerSourceType == TriggerSourceType.Emote))
            {
                trigger.TriggerSourceType = TriggerSourceType.Emote;
                configuration.Save();
            }

            if (ImGui.Selectable("Chat phrase", trigger.TriggerSourceType == TriggerSourceType.ChatPhrase))
            {
                trigger.TriggerSourceType = TriggerSourceType.ChatPhrase;
                configuration.Save();
            }

            if (ImGui.Selectable("Job skill", trigger.TriggerSourceType == TriggerSourceType.JobSkill))
            {
                trigger.TriggerSourceType = TriggerSourceType.JobSkill;
                configuration.Save();
            }

            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("What fires this trigger. Only the fields below for the selected type are used.");

        switch (trigger.TriggerSourceType)
        {
            case TriggerSourceType.Emote:
                if (DrawSearchablePicker("Emote", $"{trigger.Id}-emote", emotes, trigger.EmoteId, 0u, out var newEmoteId))
                {
                    trigger.EmoteId = newEmoteId;
                    configuration.Save();
                }

                DrawScopeCombo(trigger);
                break;

            case TriggerSourceType.ChatPhrase:
                var phrase = trigger.ChatPhrase;
                if (ImGui.InputText("Phrase", ref phrase, 128))
                {
                    trigger.ChatPhrase = phrase;
                    configuration.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Matched as a case-insensitive substring anywhere in a chat message — no fixed prefix or suffix required.");

                var chatSourcePreview = trigger.ChatTriggerSource == ChatTriggerSource.SelfTyped
                    ? "Only when I type it"
                    : "Anyone nearby";
                if (ImGui.BeginCombo("Who can say it", chatSourcePreview))
                {
                    if (ImGui.Selectable("Anyone nearby", trigger.ChatTriggerSource == ChatTriggerSource.AnyoneNearby))
                    {
                        trigger.ChatTriggerSource = ChatTriggerSource.AnyoneNearby;
                        configuration.Save();
                    }

                    if (ImGui.Selectable("Only when I type it", trigger.ChatTriggerSource == ChatTriggerSource.SelfTyped))
                    {
                        trigger.ChatTriggerSource = ChatTriggerSource.SelfTyped;
                        configuration.Save();
                    }

                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Watches whichever chat channels are enabled in the Settings tab's Watched Chat Channels section.");
                break;

            case TriggerSourceType.JobSkill:
                if (DrawSearchablePicker("Job", $"{trigger.Id}-job", jobs, trigger.JobSkillClassJobId, 0u, out var newJobId))
                {
                    trigger.JobSkillClassJobId = newJobId;
                    trigger.JobSkillActionId = 0;
                    configuration.Save();
                }

                if (trigger.JobSkillClassJobId != 0)
                {
                    var jobActions = GetJobSkills(trigger.JobSkillClassJobId);
                    if (DrawSearchablePicker("Skill", $"{trigger.Id}-jobskill", jobActions, trigger.JobSkillActionId, 0u, out var newActionId))
                    {
                        trigger.JobSkillActionId = newActionId;
                        configuration.Save();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Only actions with a cast bar are detectable this way — instant weaponskills and abilities can't be used here.");
                }

                DrawScopeCombo(trigger);
                break;
        }

        var characterNameFilter = trigger.CharacterNameFilter;
        if (ImGui.InputText("Character name filter", ref characterNameFilter, 64))
        {
            trigger.CharacterNameFilter = characterNameFilter;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Optional — restricts this trigger to one specific character by name, on top of the scope/source setting above. Leave empty to match anyone that setting already allows. Has no effect when that setting is already self-only (\"Self performed\" / \"Only when I type it\"), since there's no other character to filter among.");
    }

    private void DrawScopeCombo(ReactionTrigger trigger)
    {
        var scopePreview = trigger.Scope switch
        {
            TriggerScope.OthersTargetingMe => "Others targeting me",
            TriggerScope.SelfPerformed => "Self performed",
            TriggerScope.Anyone => "Anyone nearby",
            _ => trigger.Scope.ToString(),
        };
        if (ImGui.BeginCombo("Who triggers it", scopePreview))
        {
            if (ImGui.Selectable("Others targeting me", trigger.Scope == TriggerScope.OthersTargetingMe))
            {
                trigger.Scope = TriggerScope.OthersTargetingMe;
                configuration.Save();
            }

            if (ImGui.Selectable("Self performed", trigger.Scope == TriggerScope.SelfPerformed))
            {
                trigger.Scope = TriggerScope.SelfPerformed;
                configuration.Save();
            }

            if (ImGui.Selectable("Anyone nearby", trigger.Scope == TriggerScope.Anyone))
            {
                trigger.Scope = TriggerScope.Anyone;
                configuration.Save();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawReactionsSection(ReactionTrigger trigger)
    {
        if (DrawSearchablePicker("Glamourer design", $"{trigger.Id}-design", designs, trigger.GlamourerDesignId, Guid.Empty, out var newDesignId))
        {
            trigger.GlamourerDesignId = newDesignId;
            configuration.Save();
        }

        if (DrawMoodlePicker($"{trigger.Id}-moodle", trigger.MoodleGuid, out var newMoodleGuid))
        {
            trigger.MoodleGuid = newMoodleGuid;
            configuration.Save();
        }

        var chatMessage = trigger.ChatMessage;
        if (ImGui.InputText("Chat message", ref chatMessage, 256))
        {
            trigger.ChatMessage = chatMessage;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sent exactly as typed, e.g. \"/s hello!\" or \"/p party message\".\nA leading '/' runs it as a real command/channel switch, just like typing it yourself.");

        if (DrawSearchablePicker("Gesture", $"{trigger.Id}-gesture", gestureEmotes, trigger.GestureEmoteId, 0u, out var newGestureId))
        {
            trigger.GestureEmoteId = newGestureId;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Makes your character perform this emote once when the trigger fires. Fire-and-forget, like the chat message above — shares its send cooldown, and isn't reverted.");

        if (!string.IsNullOrWhiteSpace(trigger.ChatMessage) || trigger.GestureEmoteId != 0)
        {
            var chatCooldown = trigger.ChatCooldownSeconds;
            if (ImGui.InputInt("Chat/gesture cooldown (seconds)", ref chatCooldown))
            {
                trigger.ChatCooldownSeconds = Math.Max(0, chatCooldown);
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Minimum time between sends for this trigger's chat message and gesture combined, so a repeated trigger can't spam the channel.");
        }

        DrawPenumbraStages(trigger);

        if (!trigger.HasAnyAction)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.3f, 1f));
            ImGui.TextWrapped("Select a Glamourer design, a Moodle, a chat message, a gesture, and/or a Penumbra mod — this trigger won't do anything otherwise.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawTimingSection(ReactionTrigger trigger)
    {
        var hasMoodle = trigger.MoodleGuid != Guid.Empty;

        if (!trigger.NoExpiration)
        {
            var totalSeconds = (int)trigger.Duration.TotalSeconds;
            var durationHours = totalSeconds / 3600;
            var durationMinutes = totalSeconds % 3600 / 60;
            var durationSeconds = totalSeconds % 60;

            ImGui.TextUnformatted("Duration");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            var durationChanged = ImGui.InputInt("h##DurationHours", ref durationHours);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            durationChanged |= ImGui.InputInt("m##DurationMinutes", ref durationMinutes);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            durationChanged |= ImGui.InputInt("s##DurationSeconds", ref durationSeconds);

            if (durationChanged)
            {
                trigger.Duration = new TimeSpan(0, Math.Max(0, durationHours), Math.Max(0, durationMinutes), Math.Max(0, durationSeconds));
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How long the applied Glamourer design lasts before automatically reverting. Moodles expire on their own preset duration instead.");

            var revertModePreview = trigger.GlamourerRevertMode == GlamourerRevertMode.SpecificDesign
                ? "Apply a specific design"
                : "Revert to automation";
            if (ImGui.BeginCombo("On expiry", revertModePreview))
            {
                if (ImGui.Selectable("Revert to automation", trigger.GlamourerRevertMode == GlamourerRevertMode.Automation))
                {
                    trigger.GlamourerRevertMode = GlamourerRevertMode.Automation;
                    configuration.Save();
                }

                if (ImGui.Selectable("Apply a specific design", trigger.GlamourerRevertMode == GlamourerRevertMode.SpecificDesign))
                {
                    trigger.GlamourerRevertMode = GlamourerRevertMode.SpecificDesign;
                    configuration.Save();
                }

                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("What Glamourer state this trigger reverts to when its timer expires. \"Force revert all\" always reverts to automation regardless of this setting.");

            if (trigger.GlamourerRevertMode == GlamourerRevertMode.SpecificDesign)
            {
                if (DrawSearchablePicker("Revert-to design", $"{trigger.Id}-revert-design", designs, trigger.RevertToDesignId, Guid.Empty, out var newRevertDesignId))
                {
                    trigger.RevertToDesignId = newRevertDesignId;
                    configuration.Save();
                }
            }
        }

        ImGui.BeginDisabled(!hasMoodle);
        var noExpiration = trigger.NoExpiration;
        if (ImGui.Checkbox("No expiration##noExpiration", ref noExpiration))
        {
            trigger.NoExpiration = noExpiration;
            configuration.Save();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(hasMoodle
                ? "Never auto-revert this trigger's effects — set this by hand to match a Moodle you've configured with no expiration in Moodles. ReactToMe cannot read or sync this from Moodles automatically."
                : "Select a Moodle to enable this — it's meant to mirror a Moodle you've set to never expire.");

        var refreshOnRepeat = trigger.RefreshOnRepeat;
        if (ImGui.Checkbox("Refresh timer on repeat", ref refreshOnRepeat))
        {
            trigger.RefreshOnRepeat = refreshOnRepeat;
            configuration.Save();
        }

        ImGui.SameLine();
        var stackMultiple = trigger.StackMultiple;
        if (ImGui.Checkbox("Stack multiple", ref stackMultiple))
        {
            trigger.StackMultiple = stackMultiple;
            configuration.Save();
        }
    }

    /// <summary>Draws the Penumbra reaction section for a trigger: a None/Single/Staged mode choice that
    /// gates which fields are shown — no fields for None, flat mod/option pickers for Single (applied on
    /// first fire, no escalation), or the tabbed fire-count-threshold editor for Staged.</summary>
    private void DrawPenumbraStages(ReactionTrigger trigger)
    {
        ImGui.TextDisabled("Penumbra reaction");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("No mod reaction, a single mod/option applied on this trigger's first fire, or a fire-count-staged escalation across up to 20 stacks (FFXIV's own debuff stack limit) where each stage can target its own mod, option group, and option.");

        var effectiveMode = trigger.GetEffectivePenumbraMode();
        if (effectiveMode != trigger.PenumbraReactionMode)
        {
            trigger.PenumbraReactionMode = effectiveMode;
            configuration.Save();
        }

        var modePreview = trigger.PenumbraReactionMode switch
        {
            PenumbraReactionMode.Single => "Single mod",
            PenumbraReactionMode.Staged => "Staged escalation",
            _ => "No mod reaction",
        };
        if (ImGui.BeginCombo("Mode", modePreview))
        {
            if (ImGui.Selectable("No mod reaction", trigger.PenumbraReactionMode == PenumbraReactionMode.None))
                SetPenumbraReactionMode(trigger, PenumbraReactionMode.None);

            if (ImGui.Selectable("Single mod", trigger.PenumbraReactionMode == PenumbraReactionMode.Single))
                SetPenumbraReactionMode(trigger, PenumbraReactionMode.Single);

            if (ImGui.Selectable("Staged escalation", trigger.PenumbraReactionMode == PenumbraReactionMode.Staged))
                SetPenumbraReactionMode(trigger, PenumbraReactionMode.Staged);

            ImGui.EndCombo();
        }

        switch (trigger.PenumbraReactionMode)
        {
            case PenumbraReactionMode.Single:
                DrawPenumbraSingleMode(trigger);
                break;
            case PenumbraReactionMode.Staged:
                DrawPenumbraStagedMode(trigger);
                break;
        }
    }

    /// <summary>Applies a Penumbra reaction mode change, carrying over configuration that stays meaningful
    /// in the new mode: Single -> Staged keeps the one entry (its threshold becomes editable); Staged ->
    /// Single keeps only the first stage and re-locks its threshold to 1; switching to None clears the
    /// mod reaction entirely.</summary>
    private void SetPenumbraReactionMode(ReactionTrigger trigger, PenumbraReactionMode newMode)
    {
        if (trigger.PenumbraReactionMode == newMode)
            return;

        switch (newMode)
        {
            case PenumbraReactionMode.None:
                trigger.PenumbraStages.Clear();
                break;

            case PenumbraReactionMode.Single:
                if (trigger.PenumbraStages.Count > 1)
                    trigger.PenumbraStages.RemoveRange(1, trigger.PenumbraStages.Count - 1);
                if (trigger.PenumbraStages.Count == 0)
                    trigger.PenumbraStages.Add(new PenumbraStageThreshold { Threshold = 1 });
                else
                    trigger.PenumbraStages[0].Threshold = 1;
                break;

            case PenumbraReactionMode.Staged:
                if (trigger.PenumbraStages.Count == 0)
                    trigger.PenumbraStages.Add(new PenumbraStageThreshold { Threshold = 1 });
                break;
        }

        trigger.PenumbraReactionMode = newMode;
        configuration.Save();
    }

    /// <summary>Single-mode fields: the flat mod/option-group/option pickers with no fire-count threshold
    /// and no tab bar, backed by the sole entry in <see cref="ReactionTrigger.PenumbraStages"/> whose
    /// threshold is always locked to 1 (applied on this trigger's very first fire).</summary>
    private void DrawPenumbraSingleMode(ReactionTrigger trigger)
    {
        if (trigger.PenumbraStages.Count == 0)
            trigger.PenumbraStages.Add(new PenumbraStageThreshold { Threshold = 1 });

        var stage = trigger.PenumbraStages[0];
        if (stage.Threshold != 1)
        {
            stage.Threshold = 1;
            configuration.Save();
        }

        DrawPenumbraModFields(trigger, stage, "penumbra-single");
    }

    /// <summary>Staged-mode fields: one tab per fire-count threshold, each independently naming its own
    /// mod, option group, and option — so the same trigger can escalate within one mod's option group,
    /// across different groups of one mod, or across entirely different mods.</summary>
    private void DrawPenumbraStagedMode(ReactionTrigger trigger)
    {
        if (trigger.PenumbraStages.Count > 0 && ImGui.BeginTabBar($"##penumbraStageTabs-{trigger.Id}"))
        {
            var removeIndex = -1;
            for (var s = 0; s < trigger.PenumbraStages.Count; s++)
            {
                var stage = trigger.PenumbraStages[s];
                ImGui.PushID(s);

                var open = true;
                if (ImGui.BeginTabItem($"Stage ({stage.Threshold})###penumbraStageTab{s}", ref open))
                {
                    ImGui.SetNextItemWidth(80);
                    var threshold = stage.Threshold;
                    if (ImGui.InputInt("Fire count", ref threshold))
                    {
                        stage.Threshold = Math.Clamp(threshold, 1, ActiveEffectRegistry.MaxPenumbraFireCount);
                        configuration.Save();
                    }

                    DrawPenumbraModFields(trigger, stage, $"penumbra-stage-{s}");

                    ImGui.EndTabItem();
                }

                if (!open)
                    removeIndex = s;

                ImGui.PopID();
            }

            ImGui.EndTabBar();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Close a stage's tab to remove it. When the resolved stage's mod differs from the previous one, the previous mod is disabled first.");

            if (removeIndex >= 0)
            {
                trigger.PenumbraStages.RemoveAt(removeIndex);
                configuration.Save();
            }
        }

        if (ImGui.Button("Add Penumbra stage"))
        {
            var last = trigger.PenumbraStages.Count > 0 ? trigger.PenumbraStages[^1] : null;
            trigger.PenumbraStages.Add(new PenumbraStageThreshold
            {
                Threshold = last == null ? 1 : Math.Min(last.Threshold + 1, ActiveEffectRegistry.MaxPenumbraFireCount),
                ModDirectory = last?.ModDirectory ?? string.Empty,
                ModName = last?.ModName ?? string.Empty,
                OptionGroupName = last?.OptionGroupName ?? string.Empty,
                OptionName = string.Empty,
            });
            configuration.Save();
        }
    }

    /// <summary>Shared mod/option-group/option pickers used by both Single mode's flat fields and each
    /// Staged-mode tab's body. Not every mod has option groups at all — a mod with none is a plain
    /// enable/disable toggle, and a mod with exactly one group skips the redundant group selector and
    /// shows that group's options directly. Only a mod with two or more groups needs an explicit "Option
    /// group" chooser before its "Option" dropdown.</summary>
    private void DrawPenumbraModFields(ReactionTrigger trigger, PenumbraStageThreshold stage, string idSuffix)
    {
        if (DrawSearchablePicker("Mod", $"{trigger.Id}-{idSuffix}-mod", penumbraMods, stage.ModDirectory, string.Empty, out var newModDirectory))
        {
            stage.ModDirectory = newModDirectory;
            stage.ModName = newModDirectory.Length > 0 && penumbraMods.TryGetValue(newModDirectory, out var newModName)
                ? newModName
                : string.Empty;
            stage.OptionGroupName = string.Empty;
            stage.OptionName = string.Empty;
            configuration.Save();
        }

        if (stage.ModDirectory.Length == 0)
            return;

        var groupSettings = GetPenumbraModSettings(stage.ModDirectory, stage.ModName);

        if (groupSettings.Count == 0)
        {
            ImGui.TextDisabled("This mod has no configurable options — it will simply be enabled.");
            return;
        }

        if (groupSettings.Count == 1)
        {
            var onlyGroupName = groupSettings.Keys.First();
            if (stage.OptionGroupName != onlyGroupName)
            {
                stage.OptionGroupName = onlyGroupName;
                configuration.Save();
            }
        }
        else
        {
            var groupPreview = stage.OptionGroupName.Length > 0 ? stage.OptionGroupName : "(None)";
            if (ImGui.BeginCombo("Option group", groupPreview))
            {
                foreach (var groupName in groupSettings.Keys.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
                {
                    if (ImGui.Selectable(groupName, groupName == stage.OptionGroupName))
                    {
                        stage.OptionGroupName = groupName;
                        stage.OptionName = string.Empty;
                        configuration.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }

        if (stage.OptionGroupName.Length > 0 && groupSettings.TryGetValue(stage.OptionGroupName, out var groupInfo))
        {
            var optionPreview = stage.OptionName.Length > 0 ? stage.OptionName : "(None)";
            if (ImGui.BeginCombo("Option", optionPreview))
            {
                foreach (var optionName in groupInfo.Options)
                {
                    if (ImGui.Selectable(optionName, optionName == stage.OptionName))
                    {
                        stage.OptionName = optionName;
                        configuration.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
    }

    /// <summary>Dropdown over a live key->name list, with a search box to filter by name (some lists,
    /// like emotes, are long), alphabetical ordering, and a pinned "(None)" entry so a selection can be
    /// cleared again. Falls back to showing the raw key if the current selection isn't in the list
    /// (e.g. list not yet refreshed, or item was deleted).</summary>
    private bool DrawSearchablePicker<TKey>(string label, string searchKey, IReadOnlyDictionary<TKey, string> items, TKey current, TKey noneValue, out TKey selected)
        where TKey : notnull
    {
        selected = current;
        var isNone = EqualityComparer<TKey>.Default.Equals(current, noneValue);

        var previewValue = isNone
            ? "(None)"
            : items.TryGetValue(current, out var currentName) ? currentName : $"(unknown: {current})";

        var changed = false;
        if (ImGui.BeginCombo(label, previewValue))
        {
            searchFilters.TryGetValue(searchKey, out var filter);
            filter ??= string.Empty;
            if (ImGui.InputText("##search", ref filter, 128))
                searchFilters[searchKey] = filter;

            ImGui.Separator();

            if (ImGui.Selectable("(None)", isNone))
            {
                selected = noneValue;
                changed = true;
            }

            foreach (var (id, name) in items.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
            {
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (ImGui.Selectable(name, EqualityComparer<TKey>.Default.Equals(id, current)))
                {
                    selected = id;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private const float MoodleIconSize = 20f;

    /// <summary>Same searchable-dropdown behavior as DrawSearchablePicker (including a pinned "(None)"
    /// entry to clear the selection), but renders each moodle's icon next to its (color-parsed) title,
    /// since moodle titles can carry [color=..] style tags and the icon is otherwise the only visual
    /// distinguisher between similarly-named statuses.</summary>
    private bool DrawMoodlePicker(string searchKey, Guid current, out Guid selected)
    {
        selected = current;

        var previewValue = current == Guid.Empty
            ? "(None)"
            : moodles.TryGetValue(current, out var currentMoodle) ? MoodleTitleFormatter.StripTags(currentMoodle.RawTitle) : $"(unknown: {current})";

        var changed = false;
        if (ImGui.BeginCombo("Moodle", previewValue))
        {
            searchFilters.TryGetValue(searchKey, out var filter);
            filter ??= string.Empty;
            if (ImGui.InputText("##search", ref filter, 128))
                searchFilters[searchKey] = filter;

            ImGui.Separator();

            if (ImGui.Selectable("(None)", current == Guid.Empty))
            {
                selected = Guid.Empty;
                changed = true;
            }

            foreach (var (id, info) in moodles.OrderBy(kv => kv.Value.RawTitle, StringComparer.OrdinalIgnoreCase))
            {
                var plainTitle = MoodleTitleFormatter.StripTags(info.RawTitle);
                if (filter.Length > 0 && plainTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                ImGui.PushID(id.GetHashCode());

                var rowStart = ImGui.GetCursorPos();
                var rowHeight = MoodleIconSize;

                // Empty-label Selectable reserves the full-width clickable row; the icon and rich
                // (colored/italic) text are then drawn on top of it at the same position — plain
                // Image/Text draws never intercept clicks, so this doesn't need any overlap flag.
                if (ImGui.Selectable("##row", id == current, ImGuiSelectableFlags.None, new Vector2(0, rowHeight)))
                {
                    selected = id;
                    changed = true;
                }

                ImGui.SetCursorPos(rowStart);

                var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(info.IconId)).GetWrapOrEmpty();
                ImGui.Image(icon.Handle, new Vector2(MoodleIconSize, MoodleIconSize));
                ImGui.SameLine();

                foreach (var segment in MoodleTitleFormatter.Parse(info.RawTitle))
                {
                    var pushedColor = false;
                    if (segment.ColorRowId is { } colorRowId)
                    {
                        var color = MoodleTitleFormatter.ResolveColor(Plugin.DataManager, colorRowId);
                        if (color is { } resolvedColor)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, resolvedColor);
                            pushedColor = true;
                        }
                    }

                    ImGui.TextUnformatted(segment.Text);

                    if (pushedColor)
                        ImGui.PopStyleColor();

                    ImGui.SameLine(0, 0);
                }

                ImGui.SetCursorPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));

                ImGui.PopID();
            }

            ImGui.EndCombo();
        }

        return changed;
    }
}
