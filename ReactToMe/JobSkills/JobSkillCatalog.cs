using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ReactToMe.JobSkills;

/// <summary>Wraps the Lumina ClassJob/Action sheets to populate a job -> skill picker in the config UI.
/// Actions are filtered to player-usable, non-PvP actions that belong to a specific job, and grouped by
/// that job — the unfiltered Action sheet also contains NPC/monster actions, PvP variants, and role/general
/// actions with no single owning job, which otherwise flood a flat picker with irrelevant entries.</summary>
public sealed class JobSkillCatalog
{
    private readonly IDataManager dataManager;
    private IReadOnlyDictionary<uint, string>? jobCache;
    private IReadOnlyDictionary<uint, (string Name, uint ClassJobId)>? actionCache;

    public JobSkillCatalog(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    private IReadOnlyDictionary<uint, (string Name, uint ClassJobId)> GetActionCache()
    {
        if (actionCache != null)
            return actionCache;

        var result = new Dictionary<uint, (string, uint)>();
        var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        foreach (var row in sheet)
        {
            if (row.IsPvP || !row.IsPlayerAction)
                continue;

            var classJobId = row.ClassJob.RowId;
            if (classJobId == 0)
                continue; // Not owned by a single job (e.g. role/general actions) — out of scope.

            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[row.RowId] = (name, classJobId);
        }

        actionCache = result;
        return result;
    }

    /// <summary>Playable job RowId -> display name (e.g. "Black Mage"), for the job picker.</summary>
    public IReadOnlyDictionary<uint, string> GetJobs()
    {
        if (jobCache != null)
            return jobCache;

        var owningJobIds = GetActionCache().Values.Select(v => v.ClassJobId).ToHashSet();

        var result = new Dictionary<uint, string>();
        var sheet = dataManager.GetExcelSheet<ClassJob>();
        foreach (var row in sheet)
        {
            if (!owningJobIds.Contains(row.RowId))
                continue;

            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[row.RowId] = name;
        }

        jobCache = result;
        return result;
    }

    /// <summary>Action RowId -> display name, for the given job's own player-usable, non-PvP actions.</summary>
    public IReadOnlyDictionary<uint, string> GetActionsForJob(uint classJobId)
    {
        var result = new Dictionary<uint, string>();
        foreach (var (actionId, info) in GetActionCache())
            if (info.ClassJobId == classJobId)
                result[actionId] = info.Name;
        return result;
    }

    /// <summary>Resolves an action's display name, for labeling a trigger by its configured skill.</summary>
    public bool TryGetActionName(uint actionId, out string name)
    {
        if (GetActionCache().TryGetValue(actionId, out var info))
        {
            name = info.Name;
            return true;
        }

        name = string.Empty;
        return false;
    }
}
