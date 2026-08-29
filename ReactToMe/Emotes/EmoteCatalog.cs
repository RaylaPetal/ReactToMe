using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ReactToMe.Emotes;

/// <summary>Wraps the Lumina Emote sheet to populate an emote picker in the config UI.</summary>
public sealed class EmoteCatalog
{
    private readonly IDataManager dataManager;
    private IReadOnlyDictionary<uint, (string Name, string? Command)>? cache;

    public EmoteCatalog(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private IReadOnlyDictionary<uint, (string Name, string? Command)> GetCache()
    {
        if (cache != null)
            return cache;

        var result = new Dictionary<uint, (string, string?)>();
        var sheet = dataManager.GetExcelSheet<Emote>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var command = row.TextCommand.ValueNullable?.Command.ToString();
            result[row.RowId] = (name, string.IsNullOrWhiteSpace(command) ? null : command);
        }

        cache = result;
        return result;
    }

    /// <summary>Emote RowId (== native EmoteController.EmoteId) -> display name. Built once and cached
    /// since game data doesn't change mid-session.</summary>
    public IReadOnlyDictionary<uint, string> GetEmotes()
    {
        var result = new Dictionary<uint, string>();
        foreach (var (id, info) in GetCache())
            result[id] = info.Name;
        return result;
    }

    /// <summary>Emote RowId -> display name, for emotes with a resolvable slash command only — used for
    /// the gesture-reaction picker, since an emote without one can't be performed by sending a command.</summary>
    public IReadOnlyDictionary<uint, string> GetGestureEmotes()
    {
        var result = new Dictionary<uint, string>();
        foreach (var (id, info) in GetCache())
            if (info.Command != null)
                result[id] = info.Name;
        return result;
    }

    /// <summary>Resolves an emote's slash command (e.g. "/wave"), or null if it has none.</summary>
    public string? GetCommand(uint emoteId) => GetCache().TryGetValue(emoteId, out var info) ? info.Command : null;
}
