/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Classic.ZLevels.Roof;

public sealed partial class ClassicZLevelsRoofSystem
{
    private void InitMaps()
    {
        SubscribeLocalEvent<ClassicZMapNetworkComponent, ClassicZLevelMapNetworkUpdatedEvent>(OnMapNetworkUpdated);
    }

    private void OnMapNetworkUpdated(Entity<ClassicZMapNetworkComponent> ent, ref ClassicZLevelMapNetworkUpdatedEvent args)
    {
        RecalculateMapRoofs(ent);
    }

    private void RecalculateMapRoofs(Entity<ClassicZMapNetworkComponent> network)
    {
        _roofMap.Clear();

        foreach (var map in network.Comp.ZLevels
                     .OrderByDescending(kv => kv.Key)
                     .Select(kv => kv.Value)
                     .Where(uid => uid.HasValue)
                     .Select(uid => uid!.Value))
        {
            if (!TryComp<MapComponent>(map, out var mapComponent))
                continue;

            foreach (var grid in _mapManager.GetAllGrids(mapComponent.MapId))
            {
                var gridUid = grid.Owner;
                var roofComp = EnsureComp<RoofComponent>(gridUid);
                EnsureComp<ClassicZLevelRoofComponent>(gridUid);
                RemCompDeferred<ImplicitRoofComponent>(gridUid);

                var enumerator = Map.GetAllTilesEnumerator(gridUid, grid.Comp);
                while (enumerator.MoveNext(out var tileRef))
                {
                    var worldTile = ZLevel.GridTileToWorldTile(gridUid, grid.Comp, tileRef.Value.GridIndices);
                    Roof.SetRoof((gridUid, grid.Comp, roofComp),
                        tileRef.Value.GridIndices,
                        _roofMap.Contains(worldTile));

                    if (tileRef.Value.Tile.IsEmpty)
                        continue;

                    var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];
                    if (!tileDef.Transparent)
                        _roofMap.Add(worldTile);
                }
            }
        }
    }
}
