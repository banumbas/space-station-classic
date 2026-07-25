#pragma warning disable IDE0130 // Namespace does not match folder structure
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    internal bool TryGetClassicUpperLayers(
        Entity<SpriteComponent> sprite,
        out int northEast,
        out int northWest)
    {
        northEast = 0;
        northWest = 0;

        return _sprite.LayerMapTryGet(
                   sprite.AsNullable(),
                   CornerLayers.NE,
                   out northEast,
                   false) &&
               _sprite.LayerMapTryGet(
                   sprite.AsNullable(),
                   CornerLayers.NW,
                   out northWest,
                   false);
    }

    internal bool HasClassicNorthNeighbour(Entity<IconSmoothComponent> source)
    {
        if (!TryComp(source, out TransformComponent? transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return false;
        }

        var position = _mapSystem.TileIndicesFor(
            gridUid,
            grid,
            transform.Coordinates);

        return HasMatchingClassicNeighbour(
            source.Comp,
            _mapSystem.GetAnchoredEntitiesEnumerator(
                gridUid,
                grid,
                position.Offset(Direction.North)));
    }

    private bool HasMatchingClassicNeighbour(
        IconSmoothComponent source,
        AnchoredEntitiesEnumerator candidates)
    {
        while (candidates.MoveNext(out var candidate))
        {
            if (!TryComp(candidate, out IconSmoothComponent? other) ||
                other.SmoothKey == null ||
                !other.Enabled ||
                (other.SmoothKey != source.SmoothKey &&
                 !source.AdditionalKeys.Contains(other.SmoothKey)))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
