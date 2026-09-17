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

    /// <summary>The performing character's name, for <see cref="Triggers.ReactionTrigger.CharacterNameFilter"/>
    /// matching. Null if the actor no longer resolves by the time this is read (rare — it was just polled
    /// this same frame).</summary>
    public string? SourceName { get; init; }
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

    /// <summary>Whether <paramref name="actor"/> is currently in a looping emote animation (a solo
    /// dance-style loop, or a synced paired-emote position loop) — used to suppress a gesture
    /// reaction that would otherwise interrupt an emote already in progress (e.g. a
    /// manually-performed /hdance). Reads the native Character.Mode directly rather than
    /// EmoteController.EmoteId: EmoteId marks the moment a *new* emote starts and doesn't reliably
    /// track back to "not busy" once it ends, whereas Mode is a live state the game itself reverts
    /// to Normal the instant the loop ends or is cancelled.</summary>
    public static unsafe bool IsInEmoteLoop(IPlayerCharacter? actor)
    {
        if (actor == null)
            return false;

        var native = (Character*)actor.Address;
        if (native == null)
            return false;

        return native->Mode is CharacterModes.EmoteLoop or CharacterModes.InPositionLoop;
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
                SourceName = playerCharacter.Name.TextValue,
            });
        }
    }
}
