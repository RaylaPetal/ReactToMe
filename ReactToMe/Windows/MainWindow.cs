using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ReactToMe.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("ReactToMe##ReactToMe main window")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Show Settings"))
            plugin.ToggleConfigUi();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Active effects");

        var activeEffects = plugin.EffectRegistry.ActiveEffects;
        if (activeEffects.Count == 0)
        {
            ImGui.TextDisabled("None.");
        }
        else
        {
            var now = DateTime.UtcNow;
            var emotes = plugin.EmoteCatalog.GetEmotes();
            foreach (var effect in activeEffects)
            {
                var trigger = plugin.Configuration.Triggers.FirstOrDefault(t => t.Id == effect.TriggerId);
                var label = trigger != null && emotes.TryGetValue(trigger.EmoteId, out var emoteName)
                    ? emoteName
                    : "(unknown trigger)";
                var status = effect.ExpiresAtUtc is { } expiresAt
                    ? $"reverts in {Math.Max(0, (expiresAt - now).TotalSeconds):0}s"
                    : "no expiration";
                ImGui.TextUnformatted($"{label} — {status}");
            }

            ImGui.Spacing();
            if (ImGui.Button("Force revert all"))
                plugin.EffectRegistry.RevertAll();
        }
    }
}
