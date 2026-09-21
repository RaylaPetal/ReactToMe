using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ReactToMe.Actions;
using ReactToMe.ChatDetection;
using ReactToMe.Effects;
using ReactToMe.Emotes;
using ReactToMe.Ipc;
using ReactToMe.JobSkills;
using ReactToMe.OverlayModBuilder;
using ReactToMe.Triggers;
using ReactToMe.Windows;

namespace ReactToMe;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/reacttome";

    public Configuration Configuration { get; init; }
    public ActiveEffectRegistry EffectRegistry { get; init; }

    public readonly WindowSystem WindowSystem = new("ReactToMe");
    private ConfigWindow ConfigWindow { get; init; }

    public GlamourerIpc GlamourerIpc { get; init; }
    public MoodlesIpc MoodlesIpc { get; init; }
    public PenumbraIpc PenumbraIpc { get; init; }
    public EmoteCatalog EmoteCatalog { get; init; }
    public JobSkillCatalog JobSkillCatalog { get; init; }
    public ChatMessageSender ChatMessageSender { get; init; }
    public OverlayModBuilderService OverlayModBuilderService { get; init; }

    private readonly EmotePoller emotePoller;
    private readonly JobSkillPoller jobSkillPoller;
    private readonly ChatMessageListener chatMessageListener;

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        GlamourerIpc = new GlamourerIpc(PluginInterface, Log, ChatGui);
        MoodlesIpc = new MoodlesIpc(Log, ChatGui);
        PenumbraIpc = new PenumbraIpc(PluginInterface, Log, ChatGui);
        EffectRegistry = new ActiveEffectRegistry(GlamourerIpc, PenumbraIpc);
        EmoteCatalog = new EmoteCatalog(DataManager);
        JobSkillCatalog = new JobSkillCatalog(DataManager);
        ChatMessageSender = new ChatMessageSender(Log, ChatGui);
        OverlayModBuilderService = new OverlayModBuilderService(
            PenumbraIpc,
            new TextureCompositor(PenumbraIpc, Log),
            new OverlayModWriter(PenumbraIpc, Log),
            Log,
            ChatGui);

        emotePoller = new EmotePoller(ObjectTable, Log);
        emotePoller.EmotePerformed += OnEmotePerformed;

        jobSkillPoller = new JobSkillPoller(ObjectTable, Log);
        jobSkillPoller.JobSkillCast += OnJobSkillCast;

        chatMessageListener = new ChatMessageListener(ChatGui, ObjectTable, Configuration);
        chatMessageListener.MessageReceived += OnChatMessageReceived;

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the ReactToMe window. Use '/reacttome clear' to force-revert active effects."
        });

        PluginInterface.UiBuilder.Draw += DrawWindows;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;
        ClientState.Logout += OnLogout;

        Log.Information($"{PluginInterface.Manifest.Name} loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Logout -= OnLogout;

        emotePoller.EmotePerformed -= OnEmotePerformed;
        jobSkillPoller.JobSkillCast -= OnJobSkillCast;
        chatMessageListener.MessageReceived -= OnChatMessageReceived;
        chatMessageListener.Dispose();
        MoodlesIpc.Dispose();

        PluginInterface.UiBuilder.Draw -= DrawWindows;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        ECommonsMain.Dispose();
    }

    private void OnEmotePerformed(object? sender, EmotePerformedEventArgs e)
    {
        if (!Configuration.ReactionsEnabled)
            return;

        var localPlayer = ObjectTable.LocalPlayer;
        var localPlayerId = localPlayer?.GameObjectId;
        var sourceIsLocalPlayer = e.SourceGameObjectId == localPlayerId;
        var targetIsLocalPlayer = localPlayerId != null && e.TargetGameObjectId == localPlayerId;

        // Only meaningful for the OthersTargetingMe case — the local player is the observer being
        // measured against, so a self-performed or third-party emote has nothing to classify.
        var directionMatch = localPlayer != null && !sourceIsLocalPlayer
            ? DirectionClassifier.Classify(localPlayer.Position, localPlayer.Rotation, e.SourcePosition)
            : (DirectionMatch?)null;

        var trigger = TriggerMatcher.FindEmoteMatch(Configuration.Triggers, e.EmoteId, sourceIsLocalPlayer, targetIsLocalPlayer, e.SourceName, directionMatch);
        if (trigger != null)
            FireReactions(trigger);
    }

    private void OnJobSkillCast(object? sender, JobSkillCastEventArgs e)
    {
        if (!Configuration.ReactionsEnabled)
            return;

        var localPlayerId = ObjectTable.LocalPlayer?.GameObjectId;
        var sourceIsLocalPlayer = e.SourceGameObjectId == localPlayerId;
        var targetIsLocalPlayer = localPlayerId != null && e.TargetGameObjectId == localPlayerId;

        var trigger = TriggerMatcher.FindJobSkillMatch(Configuration.Triggers, e.ActionId, sourceIsLocalPlayer, targetIsLocalPlayer, e.SourceName);
        if (trigger != null)
            FireReactions(trigger);
    }

    private void OnChatMessageReceived(object? sender, ChatMessageReceivedEventArgs e)
    {
        if (!Configuration.ReactionsEnabled)
            return;

        var trigger = TriggerMatcher.FindChatPhraseMatch(Configuration.Triggers, e.Message, e.SenderIsLocalPlayer, e.SenderName);
        if (trigger != null)
            FireReactions(trigger);
    }

    /// <summary>Fires a trigger's reactions on demand from the config UI's "Test Fire" button, exactly as a
    /// real matched emote/chat-phrase/job-skill event would — the same <see cref="FireReactions"/> call, so
    /// there is no separate "preview" code path to keep in sync with the real one. Naturally subject to the
    /// same chat/gesture cooldown a real repeated fire would be, since both go through the same method.</summary>
    public void TestFireTrigger(ReactionTrigger trigger) => FireReactions(trigger);

    private unsafe void FireReactions(ReactionTrigger trigger)
    {
        if (trigger.GlamourerDesignId != Guid.Empty || trigger.PenumbraStages.Count > 0)
            EffectRegistry.Apply(trigger);

        if (trigger.MoodleGuid != Guid.Empty)
            MoodlesIpc.ApplyToLocalPlayer(trigger.MoodleGuid);

        // Chat message and gesture share one cooldown window (both go through the same chatbox
        // submission), so they're sent together under a single cooldown check.
        var localPlayerBusy = EmotePoller.IsInEmoteLoop(ObjectTable.LocalPlayer);
        var gestureCommand = trigger.GestureEmoteId != 0 && !localPlayerBusy
            ? EmoteCatalog.GetCommand(trigger.GestureEmoteId)
            : null;

        if (gestureCommand != null && trigger.KeepFacingOnGesture)
        {
            // Clearing the target before a targeted emote command (and restoring it right after) keeps
            // the game's native auto-face-target behavior from rotating the local player out of position
            // while the gesture plays. Safe to do unconditionally, including when there's no target to
            // begin with (clearing/restoring null is a no-op).
            var previousTarget = TargetManager.Target;
            TargetManager.Target = null;
            ChatMessageSender.Send(trigger.Id, trigger.ChatCooldownSeconds, trigger.ChatMessage, gestureCommand);
            TargetManager.Target = previousTarget;
        }
        else
        {
            ChatMessageSender.Send(trigger.Id, trigger.ChatCooldownSeconds, trigger.ChatMessage, gestureCommand);
        }
    }

    private void DrawWindows()
    {
        using var theme = PurpleTheme.Push();
        WindowSystem.Draw();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        emotePoller.Poll();
        jobSkillPoller.Poll();
        EffectRegistry.Tick();
    }

    private void OnLogout(int type, int code)
    {
        if (Configuration.RevertOnRelog)
            EffectRegistry.RevertAll();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            EffectRegistry.RevertAll();
            ChatGui.Print("[ReactToMe] Cleared active effects.");
            return;
        }

        if (args.Trim().Equals("spiketest", StringComparison.OrdinalIgnoreCase))
        {
            // Throwaway spike for the overlay-mod-builder openspec change, task 1 — see SpikeTest.cs.
            _ = OverlayModBuilder.SpikeTest.RunAsync(PluginInterface, ChatGui, Log);
            return;
        }

        if (args.Trim().Equals("spiketest reload", StringComparison.OrdinalIgnoreCase))
        {
            _ = OverlayModBuilder.SpikeTest.RunReloadAsync(PluginInterface, ChatGui, Log);
            return;
        }

        if (args.Trim().StartsWith("spiketest priority ", StringComparison.OrdinalIgnoreCase))
        {
            // Throwaway spike for the overlay-mod-builder-enhancements openspec change, task 1 — see SpikeTest.cs.
            var priorityArg = args.Trim()["spiketest priority ".Length..].Trim();
            if (int.TryParse(priorityArg, out var priority))
                OverlayModBuilder.SpikeTest.RunPriority(PluginInterface, ChatGui, Log, priority);
            else
                ChatGui.PrintError($"[ReactToMe] Spike test: \"{priorityArg}\" isn't a valid integer priority.");
            return;
        }

        ConfigWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => ConfigWindow.Toggle();
}
