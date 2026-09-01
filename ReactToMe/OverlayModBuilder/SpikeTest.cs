using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace ReactToMe.OverlayModBuilder;

/// <summary>
/// Throwaway spike (openspec change "overlay-mod-builder", task 1) verifying that a hand-authored Penumbra
/// mod folder — meta.json with one single-select group embedded inline, two options each redirecting a
/// harmless dummy game path — is actually accepted and correctly shown/toggleable by the user's installed
/// Penumbra version. Run via "/reacttome spiketest". Not the final mod writer (task 4) — this exists only
/// to get a live yes/no on the schema shape before building anything on top of it. Delete this file once
/// task 1 is confirmed and superseded by the real writer.
/// </summary>
public static class SpikeTest
{
    private const string ModDirectoryName = "ReactToMeSpikeTest";
    private const string DummyGamePath = "chara/common/texture/transparent.tex";

    public static async Task RunAsync(IDalamudPluginInterface pluginInterface, IChatGui chatGui, IPluginLog log)
    {
        try
        {
            var getModDirectory = new GetModDirectory(pluginInterface);
            var convertTextureData = new ConvertTextureData(pluginInterface);
            var addMod = new AddMod(pluginInterface);

            var penumbraModRoot = getModDirectory.Invoke();
            if (string.IsNullOrEmpty(penumbraModRoot))
            {
                chatGui.PrintError("[ReactToMe] Spike test: Penumbra did not return a mod directory — is it installed and loaded?");
                return;
            }

            var modFolder = Path.Combine(penumbraModRoot, ModDirectoryName);
            Directory.CreateDirectory(modFolder);

            chatGui.Print($"[ReactToMe] Spike test: writing test mod to \"{modFolder}\"...");

            var optionAPath = Path.Combine(modFolder, "optionA.tex");
            var optionBPath = Path.Combine(modFolder, "optionB.tex");

            var red = BuildSolidColorRgba(255, 0, 0);
            var blue = BuildSolidColorRgba(0, 0, 255);

            await convertTextureData.Invoke(red, 2, optionAPath, TextureType.RgbaTex, mipMaps: false);
            await convertTextureData.Invoke(blue, 2, optionBPath, TextureType.RgbaTex, mipMaps: false);

            if (!File.Exists(optionAPath) || !File.Exists(optionBPath))
            {
                chatGui.PrintError("[ReactToMe] Spike test: Penumbra did not produce the dummy .tex files — ConvertTextureData may have failed silently.");
                return;
            }

            var meta = new SpikeModMeta
            {
                Name = "ReactToMe Spike Test",
                Author = "ReactToMe",
                Description = "Throwaway spike test mod for the overlay-mod-builder openspec change — safe to delete.",
                Groups =
                [
                    new SpikeGroup
                    {
                        Name = "Spike Group",
                        Type = "Single",
                        Options =
                        [
                            new SpikeOption { Name = "Option A (red)", Files = { [DummyGamePath] = "optionA.tex" } },
                            new SpikeOption { Name = "Option B (blue)", Files = { [DummyGamePath] = "optionB.tex" } },
                        ],
                    },
                ],
            };

            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(modFolder, "meta.json"), metaJson);

            var result = addMod.Invoke(ModDirectoryName);
            if (result is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged)
            {
                chatGui.Print($"[ReactToMe] Spike test: AddMod returned {result}. Check Penumbra's mod list for \"ReactToMe Spike Test\" — does it show one group with two selectable options, and does selecting each one work without error?");
            }
            else
            {
                chatGui.PrintError($"[ReactToMe] Spike test: AddMod returned {result} — Penumbra rejected the mod. Check /xllog for details.");
                log.Warning("Spike test AddMod result: {Result}", result);
            }
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Spike test: Penumbra IPC unavailable");
            chatGui.PrintError("[ReactToMe] Spike test: Penumbra IPC unavailable — is Penumbra installed and loaded?");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Spike test failed");
            chatGui.PrintError($"[ReactToMe] Spike test failed: {ex.Message} — check /xllog for details.");
        }
    }

    /// <summary>Task 1.3: edits the already-registered test mod's "Option A" to point at a newly-baked
    /// green texture instead of red, and calls ReloadMod — confirming Penumbra picks up an edited option's
    /// file mapping without a manual rescan/restart. Run via "/reacttome spiketest reload" after task 1.1's
    /// mod has already been registered once.</summary>
    public static async Task RunReloadAsync(IDalamudPluginInterface pluginInterface, IChatGui chatGui, IPluginLog log)
    {
        try
        {
            var getModDirectory = new GetModDirectory(pluginInterface);
            var convertTextureData = new ConvertTextureData(pluginInterface);
            var reloadMod = new ReloadMod(pluginInterface);

            var penumbraModRoot = getModDirectory.Invoke();
            if (string.IsNullOrEmpty(penumbraModRoot))
            {
                chatGui.PrintError("[ReactToMe] Spike test: Penumbra did not return a mod directory — is it installed and loaded?");
                return;
            }

            var modFolder = Path.Combine(penumbraModRoot, ModDirectoryName);
            if (!File.Exists(Path.Combine(modFolder, "meta.json")))
            {
                chatGui.PrintError("[ReactToMe] Spike test: no existing test mod found — run \"/reacttome spiketest\" first.");
                return;
            }

            var optionAGreenPath = Path.Combine(modFolder, "optionAGreen.tex");
            var green = BuildSolidColorRgba(0, 255, 0);
            await convertTextureData.Invoke(green, 2, optionAGreenPath, TextureType.RgbaTex, mipMaps: false);

            if (!File.Exists(optionAGreenPath))
            {
                chatGui.PrintError("[ReactToMe] Spike test: Penumbra did not produce the new dummy .tex file for the reload test.");
                return;
            }

            var meta = new SpikeModMeta
            {
                Name = "ReactToMe Spike Test",
                Author = "ReactToMe",
                Description = "Throwaway spike test mod for the overlay-mod-builder openspec change — safe to delete.",
                Groups =
                [
                    new SpikeGroup
                    {
                        Name = "Spike Group",
                        Type = "Single",
                        Options =
                        [
                            new SpikeOption { Name = "Option A (now green)", Files = { [DummyGamePath] = "optionAGreen.tex" } },
                            new SpikeOption { Name = "Option B (blue)", Files = { [DummyGamePath] = "optionB.tex" } },
                        ],
                    },
                ],
            };

            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(modFolder, "meta.json"), metaJson);

            var result = reloadMod.Invoke(ModDirectoryName, "ReactToMe Spike Test");
            if (result is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged)
            {
                chatGui.Print($"[ReactToMe] Spike test: ReloadMod returned {result}. Check Penumbra — \"Option A\" should now be labeled \"Option A (now green)\" and redirect to the green texture, without needing a manual rescan or Penumbra restart.");
            }
            else
            {
                chatGui.PrintError($"[ReactToMe] Spike test: ReloadMod returned {result} — Penumbra rejected the reload. Check /xllog for details.");
                log.Warning("Spike test ReloadMod result: {Result}", result);
            }
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Spike test reload: Penumbra IPC unavailable");
            chatGui.PrintError("[ReactToMe] Spike test: Penumbra IPC unavailable — is Penumbra installed and loaded?");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Spike test reload failed");
            chatGui.PrintError($"[ReactToMe] Spike test reload failed: {ex.Message} — check /xllog for details.");
        }
    }

    /// <summary>Throwaway spike (openspec change "overlay-mod-builder-enhancements", task 1) verifying that
    /// Penumbra's <c>TrySetModPriority</c> IPC actually changes a mod's displayed priority. Run via
    /// "/reacttome spiketest priority &lt;value&gt;" after "/reacttome spiketest" has registered the test
    /// mod at least once. Delete once task 1 is confirmed and superseded by the real wiring (task 6).</summary>
    public static void RunPriority(IDalamudPluginInterface pluginInterface, IChatGui chatGui, IPluginLog log, int priority)
    {
        try
        {
            var getCollectionForObject = new GetCollectionForObject(pluginInterface);
            var trySetModPriority = new TrySetModPriority(pluginInterface);

            var (objectValid, _, collection) = getCollectionForObject.Invoke(0);
            if (!objectValid)
            {
                chatGui.PrintError("[ReactToMe] Spike test: no active Penumbra collection for your character.");
                return;
            }

            var result = trySetModPriority.Invoke(collection.Id, ModDirectoryName, priority, "ReactToMe Spike Test");
            if (result is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged)
            {
                chatGui.Print($"[ReactToMe] Spike test: TrySetModPriority returned {result}. Check \"ReactToMe Spike Test\"'s priority in Penumbra's own mod list — does it now show {priority}?");
            }
            else
            {
                chatGui.PrintError($"[ReactToMe] Spike test: TrySetModPriority returned {result} — Penumbra rejected the change.");
                log.Warning("Spike test TrySetModPriority result: {Result}", result);
            }
        }
        catch (IpcError ex)
        {
            log.Warning(ex, "Spike test priority: Penumbra IPC unavailable");
            chatGui.PrintError("[ReactToMe] Spike test: Penumbra IPC unavailable — is Penumbra installed and loaded?");
        }
    }

    private static byte[] BuildSolidColorRgba(byte r, byte g, byte b)
    {
        var pixels = new byte[2 * 2 * 4];
        for (var i = 0; i < 4; i++)
        {
            pixels[i * 4 + 0] = r;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = b;
            pixels[i * 4 + 3] = 255;
        }

        return pixels;
    }

    private sealed class SpikeModMeta
    {
        public int FileVersion { get; set; } = 4;
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "0.0.1";
        public string Website { get; set; } = string.Empty;
        public List<string> ModTags { get; set; } = [];
        public SpikeModDataContainer DefaultData { get; set; } = new();
        public List<SpikeGroup> Groups { get; set; } = [];
    }

    private sealed class SpikeModDataContainer
    {
        public Dictionary<string, string> Files { get; set; } = new();
        public Dictionary<string, string> FileSwaps { get; set; } = new();
        public List<object> Manipulations { get; set; } = [];
    }

    private sealed class SpikeGroup
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Type { get; set; } = "Single";
        public int DefaultSettings { get; set; }
        public List<SpikeOption> Options { get; set; } = [];
    }

    private sealed class SpikeOption
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public Dictionary<string, string> Files { get; set; } = new();
        public Dictionary<string, string> FileSwaps { get; set; } = new();
        public List<object> Manipulations { get; set; } = [];
    }
}
