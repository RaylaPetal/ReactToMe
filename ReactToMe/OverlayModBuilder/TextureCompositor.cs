using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using ReactToMe.Ipc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReactToMe.OverlayModBuilder;

/// <summary>
/// Reads a live game texture and alpha-composites an imported overlay image on top of it. Re-derived from
/// the reverted "penumbra-texture-overlay" change's pipeline, which was confirmed correct for these two
/// steps specifically (only that change's live-redirect application was ever the problem, not this).
/// </summary>
public sealed class TextureCompositor
{
    private readonly PenumbraIpc penumbraIpc;
    private readonly IPluginLog log;

    public TextureCompositor(PenumbraIpc penumbraIpc, IPluginLog log)
    {
        this.penumbraIpc = penumbraIpc;
        this.log = log;
    }

    /// <summary>Reads and decodes the texture currently resolving at a game path, preferring a loose file
    /// on disk (whatever mod overrides it, including one that redirects to a real absolute file path
    /// rather than a clean virtual one — checked directly, not just via the resolved actual path) and
    /// falling back to the game's own vanilla data when nothing overrides it. Decoding goes through
    /// Penumbra's own <c>ConvertTextureFile</c> IPC — the same converter Penumbra relies on for its own
    /// texture previews — rather than any decompression of our own, since Lumina's built-in BC
    /// decompression produced visibly scrambled output for at least one real body mod (likely BC7).</summary>
    public async Task<Image<Rgba32>?> ReadBaseTextureAsync(string gamePath, string actualPath)
    {
        string? tempInputFile = null;
        var tempPngFile = Path.Combine(Path.GetTempPath(), $"reacttome-overlay-base-{Guid.NewGuid()}.png");

        try
        {
            string texFilePath;
            if (File.Exists(actualPath))
            {
                texFilePath = actualPath;
            }
            else if (File.Exists(gamePath))
            {
                // Some mods redirect using a non-standard "game path" that is itself a real, direct file
                // path (e.g. an absolute path into the mod's own folder) rather than a clean virtual FFXIV
                // path with a separate ActualPath pointing at the real file.
                texFilePath = gamePath;
            }
            else
            {
                var texFile = Plugin.DataManager.GetFile<Lumina.Data.Files.TexFile>(gamePath);
                if (texFile == null)
                {
                    log.Warning(
                        "Overlay mod builder: could not resolve base texture for {GamePath} — not a loose file, not a direct file path, and not found in the game's own data either",
                        gamePath);
                    return null;
                }

                tempInputFile = Path.Combine(Path.GetTempPath(), $"reacttome-overlay-base-{Guid.NewGuid()}.tex");
                await File.WriteAllBytesAsync(tempInputFile, texFile.Data);
                texFilePath = tempInputFile;
            }

            var converted = await penumbraIpc.ConvertTextureFileAsync(texFilePath, tempPngFile, TextureType.Png);
            if (!converted)
            {
                log.Warning("Overlay mod builder: ConvertTextureFile did not produce output for {GamePath} (input {TexFilePath})", gamePath, texFilePath);
                return null;
            }

            return Image.Load<Rgba32>(tempPngFile);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Overlay mod builder: failed to read base texture for {GamePath}", gamePath);
            return null;
        }
        finally
        {
            TryDelete(tempInputFile);
            TryDelete(tempPngFile);
        }
    }

    /// <summary>Alpha-composites an imported overlay image on top of the decoded base texture, resampling
    /// the overlay to the base texture's resolution first if they differ. The result is forced fully
    /// opaque afterward: FFXIV skin diffuse textures don't necessarily use their fourth channel for real
    /// transparency (it can carry other mask data), so letting the base's own alpha propagate through the
    /// blend can leave unintended "holes" in the output. Only the overlay's alpha should control how much
    /// of it blends in — never end up as transparency in a texture that has to be fully opaque anyway.</summary>
    public static Image<Rgba32> Composite(Image<Rgba32> baseImage, string overlayImagePath)
    {
        using var overlay = Image.Load<Rgba32>(overlayImagePath);
        if (overlay.Width != baseImage.Width || overlay.Height != baseImage.Height)
            overlay.Mutate(ctx => ctx.Resize(baseImage.Width, baseImage.Height));

        var result = baseImage.Clone();
        result.Mutate(ctx => ctx.DrawImage(overlay, 1f));

        result.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x].A = 255;
            }
        });

        return result;
    }

    /// <summary>Produces the baseline stage's own baked output directly from the pristine snapshot, with no
    /// overlay involved — still forced fully opaque for the same reason <see cref="Composite"/> is, so every
    /// stage in the chain (baseline included) is consistently opaque for whatever bakes on top of it
    /// next.</summary>
    public static Image<Rgba32> Baseline(Image<Rgba32> baseImage)
    {
        var result = baseImage.Clone();
        result.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x].A = 255;
            }
        });

        return result;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort temp-file cleanup; a leftover temp file is not worth failing the bake over.
        }
    }
}
