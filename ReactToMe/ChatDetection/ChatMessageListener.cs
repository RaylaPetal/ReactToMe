using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace ReactToMe.ChatDetection;

public sealed class ChatMessageReceivedEventArgs : EventArgs
{
    public required string Message { get; init; }
    public required bool SenderIsLocalPlayer { get; init; }

    /// <summary>The message's sender name, for <see cref="Triggers.ReactionTrigger.CharacterNameFilter"/>
    /// matching.</summary>
    public required string SenderName { get; init; }
}

/// <summary>
/// Listens for incoming chat messages on the user's configured set of channel types, for chat-phrase
/// trigger matching. Whether the sender is the local player is approximated by comparing the message's
/// sender name against the local player's name — good enough for trigger matching, not a strict identity
/// check.
/// </summary>
public sealed class ChatMessageListener : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly IObjectTable objectTable;
    private readonly Configuration configuration;

    public event EventHandler<ChatMessageReceivedEventArgs>? MessageReceived;

    public ChatMessageListener(IChatGui chatGui, IObjectTable objectTable, Configuration configuration)
    {
        this.chatGui = chatGui;
        this.objectTable = objectTable;
        this.configuration = configuration;
        chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        // Read live rather than cached at construction, so a Settings change to the watched channels
        // takes effect immediately without needing a plugin reload.
        if (!configuration.WatchedChatChannels.Contains(message.LogKind))
            return;

        var localPlayerName = objectTable.LocalPlayer?.Name.TextValue;
        var senderIsLocalPlayer = localPlayerName != null
            && message.Sender.TextValue.Contains(localPlayerName, StringComparison.Ordinal);

        MessageReceived?.Invoke(this, new ChatMessageReceivedEventArgs
        {
            Message = message.Message.TextValue,
            SenderIsLocalPlayer = senderIsLocalPlayer,
            SenderName = message.Sender.TextValue,
        });
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
    }
}
