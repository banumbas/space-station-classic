/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Classic.ZLevels.Roof;

/// <summary>
/// Systems that automatically covers tiles with roofs (or removes roofs)
/// if there is a tile on one of the levels above in the ZLevels Map or Grid network.
/// </summary>
public abstract partial class ClassicSharedZLevelsRoofSystem : EntitySystem
{
    [Dependency] protected ClassicSharedZLevelsSystem ZLevel = null!;
    [Dependency] protected SharedRoofSystem Roof = null!;
    [Dependency] protected SharedMapSystem Map = null!;
    [Dependency] protected ITileDefinitionManager TilDefMan = null!;

    [Dependency] protected EntityQuery<MapGridComponent> GridQuery = default!;
    [Dependency] protected EntityQuery<RoofComponent> RoofQuery = default!;
    [Dependency] protected EntityQuery<ClassicZMapComponent> ZMapQuery = default!;
    [Dependency] protected EntityQuery<TransformComponent> XformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicZLevelRoofComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(Entity<ClassicZLevelRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;

        if (args.Changes.Length == 0)
            return;

        EntityUid? mapUid = null;
        if (ZMapQuery.HasComp(ent))
            mapUid = ent.Owner;
        else if (XformQuery.TryGetComponent(ent, out var xform))
            mapUid = xform.MapUid;

        if (mapUid is { } map && ZMapQuery.TryComp(map, out var zLevelMapComp))
            OnMapTileChanged(map, ent.Owner, currentMapGrid, currentRoof, zLevelMapComp, args);
        else
            OnGridTileChanged(ent, currentMapGrid, currentRoof, args);
    }

    /// <summary>
    /// Planetary map path: propagate roof state down through the z-map network.
    /// </summary>
    private void OnMapTileChanged(
        EntityUid mapUid,
        EntityUid currentGridUid,
        MapGridComponent currentMapGrid,
        RoofComponent currentRoof,
        ClassicZMapComponent zLevelMapComp,
        TileChangedEvent args)
    {
        Dictionary<Vector2i, bool> roofMap = new();
        foreach (var change in args.Changes)
        {
            var worldTile = ZLevel.GridTileToWorldTile(currentGridUid, currentMapGrid, change.GridIndices);
            var newTileDef = (ContentTileDefinition)TilDefMan[change.NewTile.TypeId];
            var covered = !change.NewTile.IsEmpty && !newTileDef.Transparent;

            // Preserve an explicitly managed roof on the current tile when the
            // changed tile itself did not replace it with an open tile.
            if (!covered)
                covered = Roof.IsRooved((currentGridUid, currentMapGrid, currentRoof), change.GridIndices);

            if (TryComp<MapComponent>(mapUid, out var mapComponent))
            {
                foreach (var grid in Map.GetAllGrids(mapComponent.MapId))
                {
                    if (!TryWorldTileToLocalTile(grid.Owner, grid.Comp, worldTile, out var localTile))
                        continue;

                    if (!Map.TryGetTileRef(grid.Owner, grid.Comp, localTile, out var tileRef) || tileRef.Tile.IsEmpty)
                        continue;

                    var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];
                    if (!tileDef.Transparent)
                    {
                        covered = true;
                        break;
                    }
                }
            }

            roofMap[worldTile] = covered;
        }

        var mapsBelow = ZLevel.GetAllMapsBelow((mapUid, zLevelMapComp));
        if (mapsBelow.Count == 0)
            return;

        foreach (var mapBelow in mapsBelow)
        {
            if (!TryComp<MapComponent>(mapBelow, out var mapComponentBelow))
                continue;

            var ownSolidTiles = new HashSet<Vector2i>();

            foreach (var (worldTile, rooved) in roofMap)
            {
                foreach (var gridBelow in Map.GetAllGrids(mapComponentBelow.MapId))
                {
                    if (!TryWorldTileToLocalTile(gridBelow.Owner, gridBelow.Comp, worldTile, out var localTile))
                        continue;

                    if (!Map.TryGetTileRef(gridBelow.Owner, gridBelow.Comp, localTile, out var tileRef) || tileRef.Tile.IsEmpty)
                        continue;

                    var roofBelow = EnsureComp<RoofComponent>(gridBelow.Owner);
                    EnsureComp<ClassicZLevelRoofComponent>(gridBelow.Owner);
                    Roof.SetRoof((gridBelow.Owner, gridBelow.Comp, roofBelow), localTile, rooved);

                    var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];
                    if (!tileDef.Transparent)
                        ownSolidTiles.Add(worldTile);
                }
            }

            foreach (var worldTile in ownSolidTiles)
            {
                if (roofMap.ContainsKey(worldTile))
                    roofMap[worldTile] = true;
            }
        }
    }

    private void OnGridTileChanged(
        EntityUid gridUid,
        MapGridComponent currentMapGrid,
        RoofComponent currentRoof,
        TileChangedEvent args)
    {
        if (!ZLevel.TryGetGridNetwork(gridUid, out var network))
            return;

        if (ZLevel.TryGetGridZDepth(gridUid) is not { } ownDepth)
            return;

        // Topmost first, so `covered` below only ever re-latches to true by walking downward into
        // a level with its own opaque tile — never by "seeing" a level further above.
        var below = network.Comp.Grids
            .Select(g => (Grid: g, Depth: ZLevel.TryGetGridZDepth(g)))
            .Where(x => x.Depth is { } d && d < ownDepth)
            .OrderByDescending(x => x.Depth!.Value)
            .ToList();

        if (below.Count == 0)
            return;

        foreach (var change in args.Changes)
        {
            var worldTile = ZLevel.GridTileToWorldTile(gridUid, currentMapGrid, change.GridIndices);
            var tileDef = (ContentTileDefinition)TilDefMan[change.NewTile.TypeId];
            var roovedAbove = Roof.IsRooved((gridUid, currentMapGrid, currentRoof), change.GridIndices);
            var covered = roovedAbove || !tileDef.Transparent;

            foreach (var (otherGrid, _) in below)
            {
                if (!GridQuery.TryComp(otherGrid, out var otherMapGrid))
                    continue;

                if (!TryWorldTileToLocalTile(otherGrid, otherMapGrid, worldTile, out var localTile))
                    continue;

                if (!Map.TryGetTileRef(otherGrid, otherMapGrid, localTile, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue; // nothing here on this level — neither marked nor able to re-shield below

                var otherRoof = EnsureComp<RoofComponent>(otherGrid);
                Roof.SetRoof((otherGrid, otherMapGrid, otherRoof), localTile, covered);

                var otherTileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];
                if (!otherTileDef.Transparent)
                    covered = true; // this level's own solid tile re-shields everything further down
            }
        }
    }

    /// <summary>
    /// Resolves a world tile (shared X,Y convention across Z-level maps — see
    /// <see cref="ClassicSharedZLevelsSystem"/>'s <c>TryMove</c>) into <paramref name="grid"/>'s own local
    /// tile index. Z-levels are separate MapIds, so this deliberately tags the shared (X,Y) with
    /// <paramref name="gridUid"/>'s own MapId rather than the caller's, exploiting that convention.
    /// Non-throwing: <paramref name="gridUid"/> could have been deleted between being read out of the
    /// z-grid network and this call (e.g. a ZCollapse that ate the whole grid).
    /// </summary>
    private bool TryWorldTileToLocalTile(EntityUid gridUid, MapGridComponent grid, Vector2i worldTile, out Vector2i localTile)
    {
        localTile = default;
        if (!XformQuery.TryGetComponent(gridUid, out var xform))
            return false;

        var worldPos = new Vector2(worldTile.X + 0.5f, worldTile.Y + 0.5f);
        localTile = Map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, xform.MapID));
        return true;
    }
}
