using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace ReactToMe.ChatDetection;

public sealed class ChatMessageReceivedEventArgs : EventArgs
{
    public required string Message { get; init; }
    public required bool SenderIsLocalPlayer { get; init; }
}

/// <summary>
/// Listens for incoming chat messages on a fixed set of channel types, for chat-phrase trigger matching.
/// Whether the sender is the local player is approximated by comparing the message's sender name against
/// the local player's name — good enough for trigger matching, not a strict identity check.
/// </summary>
public sealed class ChatMessageListener : IDisposable
{
    private static readonly XivChatType[] WatchedChannels =
    [
        XivChatType.Say,
        XivChatType.Yell,
        XivChatType.Shout,
        XivChatType.TellIncoming,
        XivChatType.TellOutgoing,
        XivChatType.Party,
        XivChatType.Alliance,
        XivChatType.FreeCompany,
    ];

    private readonly IChatGui chatGui;
    private readonly IObjectTable objectTable;

    public event EventHandler<ChatMessageReceivedEventArgs>? MessageReceived;

    public ChatMessageListener(IChatGui chatGui, IObjectTable objectTable)
    {
        this.chatGui = chatGui;
        this.objectTable = objectTable;
        chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (Array.IndexOf(WatchedChannels, message.LogKind) < 0)
            return;

        var localPlayerName = objectTable.LocalPlayer?.Name.TextValue;
        var senderIsLocalPlayer = localPlayerName != null
            && message.Sender.TextValue.Contains(localPlayerName, StringComparison.Ordinal);

        MessageReceived?.Invoke(this, new ChatMessageReceivedEventArgs
        {
            Message = message.Message.TextValue,
            SenderIsLocalPlayer = senderIsLocalPlayer,
        });
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
    }
}
