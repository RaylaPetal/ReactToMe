using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace ReactToMe.Emotes;

public sealed class EmotePerformedEventArgs : EventArgs
{
    public required uint EmoteId { get; init; }
    public required ulong SourceGameObjectId { get; init; }
    public required ulong TargetGameObjectId { get; init; }
}

/// <summary>
/// Detects emotes by reading each nearby player's native EmoteController.EmoteId directly off
/// their Character struct every frame (same technique NightmareXIV's DynamicBridge uses), instead
/// of via the chat log. This sidesteps the chat log entirely: it doesn't depend on the player's
/// Log Window Settings having Emote messages enabled, and it isn't gated by localized chat text.
/// </summary>
public sealed class EmotePoller
{
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;
    private readonly Dictionary<ulong, ushort> lastEmoteByActor = [];

    public event EventHandler<EmotePerformedEventArgs>? EmotePerformed;

    public EmotePoller(IObjectTable objectTable, IPluginLog log)
    {
        this.objectTable = objectTable;
        this.log = log;
    }

    public unsafe void Poll()
    {
        foreach (var obj in objectTable)
        {
            if (obj is not IPlayerCharacter playerCharacter)
                continue;

            var native = (Character*)playerCharacter.Address;
            if (native == null)
                continue;

            var emoteId = native->EmoteController.EmoteId;
            var actorId = playerCharacter.GameObjectId;

            lastEmoteByActor.TryGetValue(actorId, out var previous);
            lastEmoteByActor[actorId] = emoteId;

            if (emoteId == 0 || emoteId == previous)
                continue;

            var targetId = (ulong)native->EmoteController.Target;

            log.Information("[ReactToMe] emote polled: emoteId={EmoteId} source={Source} target={Target}",
                emoteId, actorId, targetId);

            EmotePerformed?.Invoke(this, new EmotePerformedEventArgs
            {
                EmoteId = emoteId,
                SourceGameObjectId = actorId,
                TargetGameObjectId = targetId,
            });
        }
    }
}
