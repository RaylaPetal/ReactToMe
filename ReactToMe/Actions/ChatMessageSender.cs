using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons.Automation;

namespace ReactToMe.Actions;

/// <summary>
/// Sends one or more chat messages/commands exactly as if typed and submitted (via
/// ECommons.Automation.Chat, which goes through the game's own chatbox entry point) — so leading "/"
/// runs a real command or channel switch (e.g. "/s", "/p"). Per-trigger cooldown guards against tripping
/// the game's own chat spam throttle if the trigger's condition repeats rapidly.
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

    /// <summary>Sends every non-empty message in <paramref name="messages"/> under a single cooldown
    /// check, so a trigger's chat-message and gesture reactions share one cooldown window instead of the
    /// first send's timestamp update immediately blocking the second for the same fire.</summary>
    public void Send(Guid triggerId, int cooldownSeconds, params string?[] messages)
    {
        var toSend = messages.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).ToList();
        if (toSend.Count == 0)
            return;

        var now = DateTime.UtcNow;
        if (lastSentByTrigger.TryGetValue(triggerId, out var lastSent) && (now - lastSent).TotalSeconds < cooldownSeconds)
            return;

        var sentAny = false;
        foreach (var message in toSend)
        {
            try
            {
                Chat.SendMessage(message);
                sentAny = true;
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to send chat message \"{Message}\" for trigger {TriggerId}", message, triggerId);
                chatGui.PrintError("[ReactToMe] Could not send chat message — check it isn't too long or doesn't contain invalid characters.");
            }
        }

        if (sentAny)
            lastSentByTrigger[triggerId] = now;
    }
}
