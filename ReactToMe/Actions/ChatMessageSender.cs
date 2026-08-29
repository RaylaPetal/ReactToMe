using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.Automation;

namespace ReactToMe.Actions;

/// <summary>
/// Sends a chat message/command exactly as if typed and submitted (via ECommons.Automation.Chat,
/// which goes through the game's own chatbox entry point) — so leading "/" runs a real command or
/// channel switch (e.g. "/s", "/p"). Per-trigger cooldown guards against tripping the game's own
/// chat spam throttle if the trigger's condition repeats rapidly.
/// </summary>
public sealed class ChatMessageSender
{
    private readonly IPluginLog log;
    private readonly IChatGui chatGui;
    private readonly Dictionary<Guid, DateTime> lastSentByTrigger = new();

    public ChatMessageSender(IPluginLog log, IChatGui chatGui)
    {
        this.log = log;
        this.chatGui = chatGui;
    }

    public void Send(Guid triggerId, string message, int cooldownSeconds)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var now = DateTime.UtcNow;
        if (lastSentByTrigger.TryGetValue(triggerId, out var lastSent) && (now - lastSent).TotalSeconds < cooldownSeconds)
            return;

        try
        {
            Chat.SendMessage(message);
            lastSentByTrigger[triggerId] = now;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to send chat message \"{Message}\" for trigger {TriggerId}", message, triggerId);
            chatGui.PrintError("[ReactToMe] Could not send chat message — check it isn't too long or doesn't contain invalid characters.");
        }
    }
}
