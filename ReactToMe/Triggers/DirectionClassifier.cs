using System;
using System.Numerics;

namespace ReactToMe.Triggers;

/// <summary>Where a source actor is standing relative to an observer's position and facing, for
/// <see cref="DirectionFilter"/> matching.</summary>
public enum DirectionMatch
{
    /// <summary>Within the 120°-wide arc centered directly in front of the observer.</summary>
    InFront,

    /// <summary>Within the 120°-wide arc centered directly behind the observer.</summary>
    Behind,

    /// <summary>In one of the two remaining 60°-wide arcs to either side — neither in front nor behind.</summary>
    Side,
}

/// <summary>
/// Classifies a source actor's bearing relative to an observer's position and facing, for
/// <see cref="ReactionTrigger.DirectionFilter"/> matching. Only the horizontal (X/Z) plane is
/// considered — vertical offset (e.g. standing on stairs) doesn't affect the classification.
///
/// The forward vector is derived from <see cref="Dalamud.Game.ClientState.Objects.Types.IGameObject.Rotation"/>
/// (-pi..pi radians) as (sin(rotation), cos(rotation)) in the X/Z plane. This sign convention hasn't been
/// confirmed against the live game client — if in-game testing shows <see cref="DirectionMatch.Behind"/>
/// and <see cref="DirectionMatch.InFront"/> are swapped, negate <c>observerRotation</c> (or equivalently
/// swap the sin/cos below) to correct it; <see cref="DirectionMatch.Side"/> is unaffected either way since
/// it's symmetric around both the forward and rear directions.
/// </summary>
public static class DirectionClassifier
{
    /// <summary>Half-width, in degrees, of both the front and rear cones — matches FFXIV's own
    /// rear-positional convention.</summary>
    private const float ConeHalfWidthDegrees = 60f;

    public static DirectionMatch Classify(Vector3 observerPosition, float observerRotation, Vector3 sourcePosition)
    {
        var toSource = sourcePosition - observerPosition;
        var toSourceFlat = new Vector2(toSource.X, toSource.Z);
        if (toSourceFlat.LengthSquared() < 0.0001f)
            return DirectionMatch.InFront; // degenerate: source is effectively on top of the observer

        toSourceFlat = Vector2.Normalize(toSourceFlat);
        var forward = new Vector2(MathF.Sin(observerRotation), MathF.Cos(observerRotation));

        var dot = Math.Clamp(Vector2.Dot(forward, toSourceFlat), -1f, 1f);
        var thetaDegrees = MathF.Acos(dot) * (180f / MathF.PI);

        if (thetaDegrees <= ConeHalfWidthDegrees)
            return DirectionMatch.InFront;
        if (thetaDegrees >= 180f - ConeHalfWidthDegrees)
            return DirectionMatch.Behind;
        return DirectionMatch.Side;
    }
}
