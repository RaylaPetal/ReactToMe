using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ReactToMe.Windows;

public readonly record struct MoodleTitleSegment(string Text, uint? ColorRowId, bool Italic);

/// <summary>
/// Moodle status titles carry inline style tags — [color=N]/[/color] (also accepts named colors from
/// ECommons' UIColor enum), [glow=N]/[/glow], and [i]/[/i] — the exact set Moodles' own Utils.ParseBBSeString
/// supports (confirmed against kawaii/Moodles source). N is a row id into the game's own Lumina UIColor
/// sheet, not a Moodles-owned palette. This mirrors that same flat/sequential tag-state parsing so titles
/// render with real colors/italics instead of showing the raw brackets or just dropping them.
/// </summary>
public static partial class MoodleTitleFormatter
{
    [GeneratedRegex(@"\[color=(?<color>[0-9a-zA-Z]+)\]|\[/color\]|\[glow=[0-9a-zA-Z]+\]|\[/glow\]|\[i\]|\[/i\]", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    public static string StripTags(string rawTitle) => TagRegex().Replace(rawTitle, string.Empty);

    public static IReadOnlyList<MoodleTitleSegment> Parse(string rawTitle)
    {
        var segments = new List<MoodleTitleSegment>();
        uint? currentColor = null;
        var italic = false;
        var lastIndex = 0;

        foreach (var match in TagRegex().EnumerateMatches(rawTitle))
        {
            var tag = rawTitle.Substring(match.Index, match.Length);

            if (match.Index > lastIndex)
            {
                var text = rawTitle[lastIndex..match.Index];
                if (text.Length > 0)
                    segments.Add(new MoodleTitleSegment(text, currentColor, italic));
            }

            if (tag.StartsWith("[color=", StringComparison.OrdinalIgnoreCase))
            {
                var name = tag[7..^1];
                currentColor = ResolveColorRowId(name);
            }
            else if (tag.Equals("[/color]", StringComparison.OrdinalIgnoreCase))
            {
                currentColor = null;
            }
            else if (tag.Equals("[i]", StringComparison.OrdinalIgnoreCase))
            {
                italic = true;
            }
            else if (tag.Equals("[/i]", StringComparison.OrdinalIgnoreCase))
            {
                italic = false;
            }
            // [glow=..]/[/glow] tags are consumed (stripped from visible text) but not separately
            // rendered — ImGui has no text glow/outline effect to match FFXIV's native one.

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < rawTitle.Length)
            segments.Add(new MoodleTitleSegment(rawTitle[lastIndex..], currentColor, italic));

        return segments;
    }

    private static uint? ResolveColorRowId(string value)
    {
        if (uint.TryParse(value, out var id))
            return id;

        // Moodles also accepts named colors, resolved against the same row ids via ECommons' enum.
        if (Enum.TryParse<ECommons.ChatMethods.UIColor>(value, true, out var named))
            return (uint)named;

        return null;
    }

    /// <summary>Resolves a UIColor sheet row id to an ImGui-usable RGBA color. UIColor.Dark is packed
    /// as 0xRRGGBBAA (FFXIV's native SeString color convention).</summary>
    public static Vector4? ResolveColor(IDataManager dataManager, uint rowId)
    {
        var row = dataManager.GetExcelSheet<UIColor>().GetRowOrDefault(rowId);
        if (row == null)
            return null;

        var packed = row.Value.Dark;
        var r = (byte)(packed >> 24);
        var g = (byte)(packed >> 16);
        var b = (byte)(packed >> 8);
        var a = (byte)packed;
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
