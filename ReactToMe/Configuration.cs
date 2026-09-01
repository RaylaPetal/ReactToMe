using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using ReactToMe.OverlayModBuilder;
using ReactToMe.Triggers;

namespace ReactToMe;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;

    public List<ReactionTrigger> Triggers { get; set; } = [];

    public List<OverlayModBuilderProject> OverlayModBuilderProjects { get; set; } = [];

    /// <summary>
    /// If true, an active Glamourer effect is reverted immediately on logout instead of
    /// resuming/expiring on wall-clock time after the next login (doc §5's relog question;
    /// v1 defaults to the simpler "implicit revert on logout" behavior).
    /// </summary>
    public bool RevertOnRelog { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
