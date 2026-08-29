using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Penumbra.Api.Enums;
using ReactToMe.Effects;
using ReactToMe.Ipc;
using ReactToMe.JobSkills;
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
    private bool listsLoaded;

    private Guid? selectedTriggerId;
    private string triggerListFilter = string.Empty;

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

    public void Dispose() { }

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

            if (ImGui.BeginTabItem("Triggers"))
            {
                DrawTriggersTab();
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
                ? $"reverts in {Math.Max(0, (expiresAt - now).TotalSeconds):0}s"
                : "no expiration";
            ImGui.TextUnformatted($"{label} — {status}");
        }

        ImGui.Spacing();
        if (ImGui.Button("Force revert all"))
            plugin.EffectRegistry.RevertAll();
    }

    private void DrawSettingsTab()
    {
        ImGui.Spacing();

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
            var newTrigger = new ReactionTrigger();
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
        if (ImGui.Button("Remove Trigger"))
        {
            configuration.Triggers.Remove(trigger);
            selectedTriggerId = null;
            configuration.Save();
            ImGui.PopID();
            return;
        }

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
                    ImGui.SetTooltip("Watches Say, Yell, Shout, Tell, Party, Alliance, and Free Company chat only.");
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
            var durationMinutes = (float)trigger.Duration.TotalMinutes;
            if (ImGui.InputFloat("Duration (minutes)", ref durationMinutes))
            {
                trigger.Duration = TimeSpan.FromMinutes(Math.Max(0, durationMinutes));
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
