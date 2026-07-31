/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Classic.ZLevels.Mapping;

public sealed partial class ClassicZLevelMappingSystem : EntitySystem
{
    private static readonly Color PlanetaryLightColor = Color.FromHex("#D8B059");

    [Dependency] private ClassicZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
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
        EnsurePlanetaryLighting(ent.Owner, network.Comp);
    }

    private void EnsurePlanetaryLighting(EntityUid mapUid, ClassicZMapNetworkComponent network)
    {
        var color = PlanetaryLightColor;
        if (network.ZLevels.TryGetValue(0, out var baseMap) && baseMap is { } baseLightMapUid &&
            TryComp<MapLightComponent>(baseLightMapUid, out var baseLight))
        {
            color = baseLight.AmbientLightColor;
        }

        var baseMapUid = network.ZLevels.TryGetValue(0, out var baseMapEntry) ? baseMapEntry : null;
        var baseCycle = baseMapUid is { } baseMapEntity
            ? EnsureMapLightCycle(baseMapEntity, color)
            : null;

        var cycle = EnsureMapLightCycle(mapUid, color);
        if (baseCycle is { } sharedCycle && mapUid != baseMapUid)
        {
            cycle.Offset = sharedCycle.Offset;
            cycle.Duration = sharedCycle.Duration;
            cycle.InitialOffset = false;
            Dirty(mapUid, cycle);
        }

        EnsureComp<SunShadowComponent>(mapUid);
        var mapShadowCycle = EnsureComp<SunShadowCycleComponent>(mapUid);
        if (baseCycle is { } sharedMapCycle)
        {
            mapShadowCycle.Offset = sharedMapCycle.Offset;
            mapShadowCycle.Duration = sharedMapCycle.Duration;
            Dirty(mapUid, mapShadowCycle);
        }

        // Map grids are the actual light surfaces. A map can have several grids
        // (planet, station, and z-level geometry), so keep all of them in the
        // planetary-light pipeline.
        if (!TryComp<MapComponent>(mapUid, out var mapComponent))
            return;

        foreach (var grid in _mapManager.GetAllGrids(mapComponent.MapId))
        {
            EnsureComp<RoofComponent>(grid.Owner);
            // The loaded map prototypes commonly contain ImplicitRoof. It
            // overrides the per-tile roof mask and makes the whole z-level
            // dark, including the top level where there is no roof above it.
            RemCompDeferred<ImplicitRoofComponent>(grid.Owner);
            EnsureComp<Content.Shared._Classic.ZLevels.Roof.ClassicZLevelRoofComponent>(grid.Owner);
            EnsureComp<SunShadowComponent>(grid.Owner);
            var shadowCycle = EnsureComp<SunShadowCycleComponent>(grid.Owner);
            if (baseCycle is { } sharedGridCycle)
            {
                shadowCycle.Offset = sharedGridCycle.Offset;
                shadowCycle.Duration = sharedGridCycle.Duration;
                Dirty(grid.Owner, shadowCycle);
            }
        }
    }

    private LightCycleComponent EnsureMapLightCycle(EntityUid mapUid, Color fallbackColor)
    {
        var mapLight = EnsureComp<MapLightComponent>(mapUid);
        if (mapLight.AmbientLightColor == MapLightComponent.DefaultColor)
        {
            mapLight.AmbientLightColor = fallbackColor;
            Dirty(mapUid, mapLight);
        }

        var cycle = EnsureComp<LightCycleComponent>(mapUid);
        if (cycle.OriginalColor == Color.Transparent)
            cycle.OriginalColor = mapLight.AmbientLightColor;

        // Components added to a map after MapInit never receive the normal
        // random-offset initialization. All maps in one z-network must use
        // the same phase instead of independently starting day/night cycles.
        cycle.InitialOffset = false;
        Dirty(mapUid, cycle);
        return cycle;
    }
}
