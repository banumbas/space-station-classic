/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Linq;

namespace Content.Server._Classic.ZLevels.Mapping;

public sealed partial class ClassicZLevelMappingSystem : EntitySystem
{
    private static readonly Color PlanetaryLightColor = Color.FromHex("#D8B059");

    [Dependency] private ClassicZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IMapManager _mapManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicZMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClassicZMapComponent, ClassicMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<ClassicZMapComponent> ent, ref ClassicMapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
        {
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
            AlignMapGrids(ent.Owner, args.Network.Comp);
            EnsurePlanetaryLighting(ent.Owner, args.Network.Comp);
        }
        else
        {
            var hasInitializedMaps = false;
            foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
            {
                if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
                {
                    hasInitializedMaps = true;
                    break;
                }
            }

            if (hasInitializedMaps)
                _map.InitializeMap(ent.Owner);
        }
    }

    private void OnMapInit(Entity<ClassicZMapComponent> ent, ref MapInitEvent args)
    {
        if (!_zLevels.TryGetMapNetwork(ent, out var network))
            return;

        EntityManager.AddComponents(ent, network.Comp.Components);
        AlignMapGrids(ent.Owner, network.Comp);
        EnsurePlanetaryLighting(ent.Owner, network.Comp);
    }

    /// <summary>
    /// Z-level maps share tile coordinates. Keep their primary grids on the same
    /// world origin as the depth-0 grid, while preserving the relative placement
    /// of any additional grids on the map.
    /// </summary>
    private void AlignMapGrids(EntityUid mapUid, ClassicZMapNetworkComponent network)
    {
        if (!network.ZLevels.TryGetValue(0, out var baseMapUid) ||
            baseMapUid is not { } baseMap || baseMap == mapUid ||
            !TryComp<MapComponent>(baseMap, out var baseMapComponent) ||
            !TryComp<MapComponent>(mapUid, out var mapComponent))
            return;

        var baseGrid = _mapManager.GetAllGrids(baseMapComponent.MapId)
            .OrderByDescending(grid => grid.Comp.LocalAABB.Size.LengthSquared())
            .FirstOrDefault();
        var anchorGrid = _mapManager.GetAllGrids(mapComponent.MapId)
            .OrderByDescending(grid => grid.Comp.LocalAABB.Size.LengthSquared())
            .FirstOrDefault();

        if (baseGrid.Owner == EntityUid.Invalid || anchorGrid.Owner == EntityUid.Invalid)
            return;

        var basePosition = _transform.GetWorldPosition(baseGrid.Owner);
        var baseRotation = _transform.GetWorldRotation(baseGrid.Owner);
        var anchorPosition = _transform.GetWorldPosition(anchorGrid.Owner);
        var anchorRotation = _transform.GetWorldRotation(anchorGrid.Owner);

        foreach (var grid in _mapManager.GetAllGrids(mapComponent.MapId))
        {
            var gridPosition = _transform.GetWorldPosition(grid.Owner);
            var gridRotation = _transform.GetWorldRotation(grid.Owner);
            var relativePosition = new Angle(-anchorRotation.Theta).RotateVec(gridPosition - anchorPosition);
            var relativeRotation = gridRotation - anchorRotation;

            _transform.SetWorldPositionRotation(
                grid.Owner,
                basePosition + baseRotation.RotateVec(relativePosition),
                baseRotation + relativeRotation);
        }
    }

    private void EnsurePlanetaryLighting(EntityUid mapUid, ClassicZMapNetworkComponent network)
    {
        var color = PlanetaryLightColor;
        if (network.ZLevels.TryGetValue(0, out var baseMap) && baseMap is { } baseMapUid &&
            TryComp<MapLightComponent>(baseMapUid, out var baseLight))
        {
            color = baseLight.AmbientLightColor;
        }

        if (!TryComp<MapLightComponent>(mapUid, out var mapLight))
        {
            mapLight = EnsureComp<MapLightComponent>(mapUid);
            mapLight.AmbientLightColor = color;
            Dirty(mapUid, mapLight);
        }
        else if (mapLight.AmbientLightColor == MapLightComponent.DefaultColor)
        {
            mapLight.AmbientLightColor = color;
            Dirty(mapUid, mapLight);
        }

        var cycle = EnsureComp<LightCycleComponent>(mapUid);
        if (cycle.OriginalColor == Color.Transparent)
        {
            cycle.OriginalColor = mapLight.AmbientLightColor;
            Dirty(mapUid, cycle);
        }

        EnsureComp<SunShadowComponent>(mapUid);
        EnsureComp<SunShadowCycleComponent>(mapUid);

        // Map grids are the actual light surfaces. A map can have several grids
        // (planet, station, and z-level geometry), so keep all of them in the
        // planetary-light pipeline.
        if (!TryComp<MapComponent>(mapUid, out var mapComponent))
            return;

        foreach (var grid in _mapManager.GetAllGrids(mapComponent.MapId))
        {
            EnsureComp<RoofComponent>(grid.Owner);
            EnsureComp<Content.Shared._Classic.ZLevels.Roof.ClassicZLevelRoofComponent>(grid.Owner);
            EnsureComp<SunShadowComponent>(grid.Owner);
            EnsureComp<SunShadowCycleComponent>(grid.Owner);
        }
    }
}
