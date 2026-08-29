using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ReactToMe.Actions;
using ReactToMe.Effects;
using ReactToMe.Emotes;
using ReactToMe.Ipc;
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
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/reacttome";

    public Configuration Configuration { get; init; }
    public ActiveEffectRegistry EffectRegistry { get; init; }

    public readonly WindowSystem WindowSystem = new("ReactToMe");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public GlamourerIpc GlamourerIpc { get; init; }
    public MoodlesIpc MoodlesIpc { get; init; }
    public PenumbraIpc PenumbraIpc { get; init; }
    public EmoteCatalog EmoteCatalog { get; init; }
    public ChatMessageSender ChatMessageSender { get; init; }

    private readonly EmotePoller emotePoller;

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        GlamourerIpc = new GlamourerIpc(PluginInterface, Log, ChatGui);
        MoodlesIpc = new MoodlesIpc(Log, ChatGui);
        PenumbraIpc = new PenumbraIpc(PluginInterface, Log, ChatGui);
        EffectRegistry = new ActiveEffectRegistry(GlamourerIpc, PenumbraIpc);
        EmoteCatalog = new EmoteCatalog(DataManager);
        ChatMessageSender = new ChatMessageSender(Log, ChatGui);

        emotePoller = new EmotePoller(ObjectTable, Log);
        emotePoller.EmotePerformed += OnEmotePerformed;

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

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
        MoodlesIpc.Dispose();

        PluginInterface.UiBuilder.Draw -= DrawWindows;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        ECommonsMain.Dispose();
    }

    private void OnEmotePerformed(object? sender, EmotePerformedEventArgs e)
    {
        var localPlayerId = ObjectTable.LocalPlayer?.GameObjectId;
        var sourceIsLocalPlayer = e.SourceGameObjectId == localPlayerId;
        var targetIsLocalPlayer = localPlayerId != null && e.TargetGameObjectId == localPlayerId;

        var trigger = TriggerMatcher.FindMatch(Configuration.Triggers, e.EmoteId, sourceIsLocalPlayer, targetIsLocalPlayer);
        if (trigger == null)
            return;

        if (trigger.GlamourerDesignId != Guid.Empty || trigger.PenumbraStages.Count > 0)
            EffectRegistry.Apply(trigger);

        if (trigger.MoodleGuid != Guid.Empty)
            MoodlesIpc.ApplyToLocalPlayer(trigger.MoodleGuid);

        if (!string.IsNullOrWhiteSpace(trigger.ChatMessage))
            ChatMessageSender.Send(trigger.Id, trigger.ChatMessage, trigger.ChatCooldownSeconds);
    }

    private void DrawWindows()
    {
        using var theme = PurpleTheme.Push();
        WindowSystem.Draw();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        emotePoller.Poll();
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

        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
