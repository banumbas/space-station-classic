// Namespace does not match folder structure
#pragma warning disable IDE0130
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    private const string DiggingQuality = "Digging";
    private const int LowestDiggableZLevel = -2;

    /// <summary>
    /// The two lowest Classic Z-levels are bedrock and cannot be modified by a digging tool.
    /// </summary>
    private bool CanDigClassicTile(TileRef tileRef, PrototypeFlags<ToolQualityPrototype> toolQualities)
    {
        if (!toolQualities.Contains(DiggingQuality))
            return true;

        var mapUid = Transform(tileRef.GridUid).MapUid;
        return mapUid == null ||
               !TryComp<ClassicZMapComponent>(mapUid.Value, out var zMap) ||
               zMap.Depth > LowestDiggableZLevel;
    }
}

public readonly record struct ClassicDiggingTileDeconstructedEvent(EntityUid GridUid, Vector2i GridIndices);
