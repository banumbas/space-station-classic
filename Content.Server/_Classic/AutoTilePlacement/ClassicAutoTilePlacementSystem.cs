/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Classic.AutoTilePlacement;

public sealed partial class ClassicAutoTilePlacementSystem : EntitySystem
{
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private ITileDefinitionManager _tiledef = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicAutoTilePlacementComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<ClassicAutoTilePlacementComponent> ent, ref ComponentInit args)
    {
        var xform = Transform(ent);
        var coord = xform.Coordinates;
        var gridUid = xform.GridUid;
        var proto = MetaData(ent).EntityPrototype;

        if (gridUid is null)
            return;

        if (proto is null)
            return;

        if (!TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var tileRef = _maps.GetTileRef(gridUid.Value, grid, coord);

        if (!tileRef.Tile.IsEmpty)
            return;

        // Resolve the tile definition and replace the empty tile with the configured one.
        if (!_tiledef.TryGetDefinition(ent.Comp.Tile, out var def))
            return;

        _tile.ReplaceTile(tileRef, (ContentTileDefinition) def);

        //Recreate this entity
        Spawn(proto.ID, xform.Coordinates);
        QueueDel(ent);
    }
}
