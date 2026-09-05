// Namespace does not match folder structure
#pragma warning disable IDE0130
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    private const string DiggingQuality = "Digging";
    private const int LowestDiggableZLevel = -3;

    /// <summary>
    /// Natural layers may still be stripped at the bottom Z-level, but the last bedrock layer
    /// cannot be turned into open space by a digging tool.
    /// </summary>
    private bool CanDigClassicTile(TileRef tileRef, PrototypeFlags<ToolQualityPrototype> toolQualities)
    {
        if (!toolQualities.Contains(DiggingQuality))
            return true;

        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];
        if (!tileDef.NaturalTerrain || string.IsNullOrWhiteSpace(tileDef.BaseTurf))
            return true;

        var baseTurf = (ContentTileDefinition) _tileDefManager[tileDef.BaseTurf];
        if (baseTurf.TileId != 0 && !baseTurf.MapAtmosphere)
            return true;

        var mapUid = Transform(tileRef.GridUid).MapUid;
        return mapUid == null ||
               !TryComp<ClassicZMapComponent>(mapUid.Value, out var zMap) ||
               zMap.Depth > LowestDiggableZLevel;
    }
}
