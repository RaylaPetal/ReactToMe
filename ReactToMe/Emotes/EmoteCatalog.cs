using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ReactToMe.Emotes;

/// <summary>Wraps the Lumina Emote sheet to populate an emote picker in the config UI.</summary>
public sealed class EmoteCatalog
{
    private readonly IDataManager dataManager;
    private IReadOnlyDictionary<uint, string>? cache;

    public EmoteCatalog(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    /// <summary>Emote RowId (== native EmoteController.EmoteId) -> display name. Built once and cached
    /// since game data doesn't change mid-session.</summary>
    public IReadOnlyDictionary<uint, string> GetEmotes()
    {
        if (cache != null)
            return cache;

        var result = new Dictionary<uint, string>();
        var sheet = dataManager.GetExcelSheet<Emote>();
        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[row.RowId] = name;
        }

        cache = result;
        return result;
    }
}
