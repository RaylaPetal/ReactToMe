using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ReactToMe.Ipc;
using ReactToMe.Triggers;

namespace ReactToMe.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private readonly Dictionary<string, string> searchFilters = new();

    private IReadOnlyDictionary<uint, string> emotes = new Dictionary<uint, string>();
    private IReadOnlyDictionary<Guid, string> designs = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, MoodleInfo> moodles = new Dictionary<Guid, MoodleInfo>();
    private bool listsLoaded;

    public ConfigWindow(Plugin plugin) : base("ReactToMe Configuration###ReactToMe config window")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(580, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(640, 480);
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
        designs = plugin.GlamourerIpc.GetDesigns();
        moodles = plugin.MoodlesIpc.GetMoodles();
        listsLoaded = true;
    }

    public override void Draw()
    {
        if (!listsLoaded)
            RefreshLists();

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

        ImGui.Separator();
        ImGui.TextUnformatted($"Triggers ({configuration.Triggers.Count})");

        if (ImGui.Button("Refresh lists"))
            RefreshLists();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Re-fetches the emote list and your current Glamourer designs / Moodles — use this after creating a new one in-game.");

        var removeIndex = -1;

        for (var i = 0; i < configuration.Triggers.Count; i++)
        {
            var trigger = configuration.Triggers[i];
            ImGui.PushID(i);

            var emoteName = emotes.TryGetValue(trigger.EmoteId, out var name) ? name : "(no emote selected)";
            var headerLabel = trigger.IsEnabled ? emoteName : $"{emoteName} (disabled)";
            var headerOpen = ImGui.CollapsingHeader($"{headerLabel}###triggerHeader", ImGuiTreeNodeFlags.DefaultOpen);

            if (headerOpen)
            {
                ImGui.Indent();

                var enabled = trigger.IsEnabled;
                if (ImGui.Checkbox("Enabled##enabled", ref enabled))
                {
                    trigger.IsEnabled = enabled;
                    configuration.Save();
                }

                ImGui.SameLine();
                if (ImGui.Button("Remove"))
                    removeIndex = i;

                if (DrawSearchablePicker("Emote", $"{trigger.Id}-emote", emotes, trigger.EmoteId, 0u, out var newEmoteId))
                {
                    trigger.EmoteId = newEmoteId;
                    configuration.Save();
                }

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

                ImGui.Spacing();
                ImGui.TextDisabled("Actions (at least one required)");

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

                if (!string.IsNullOrWhiteSpace(trigger.ChatMessage))
                {
                    var chatCooldown = trigger.ChatCooldownSeconds;
                    if (ImGui.InputInt("Chat cooldown (seconds)", ref chatCooldown))
                    {
                        trigger.ChatCooldownSeconds = Math.Max(0, chatCooldown);
                        configuration.Save();
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Minimum time between chat sends for this trigger, so a repeated emote can't spam the channel.");
                }

                if (!trigger.HasAnyAction)
                    ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), "Select a Glamourer design, a Moodle, and/or a chat message — this trigger won't do anything otherwise.");

                ImGui.Spacing();

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

                ImGui.Unindent();
            }

            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            configuration.Triggers.RemoveAt(removeIndex);
            configuration.Save();
        }

        ImGui.Separator();
        if (ImGui.Button("Add Trigger"))
        {
            configuration.Triggers.Add(new ReactionTrigger());
            configuration.Save();
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
