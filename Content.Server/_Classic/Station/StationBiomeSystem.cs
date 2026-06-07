using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Classic.Station;

/// <summary>
/// Creates a planet biome on the station grid itself so tile/node/power systems
/// keep seeing one connected grid.
/// </summary>
public sealed partial class ClassicStationBiomeSystem : EntitySystem
{
    private static readonly GasMixture PlanetAtmosphere = CreatePlanetAtmosphere();

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tile = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicStationBiomeComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<ClassicStationBiomeComponent> ent, ref StationPostInitEvent args)
    {
        var stationUid = args.Station.Owner;
        var stationGrid = _station.GetLargestGrid(stationUid);
        if (stationGrid == null)
            return;

        var stationGridUid = stationGrid.Value;
        var stationGridXform = Transform(stationGridUid);
        var mapUid = _map.GetMapOrInvalid(stationGridXform.MapID);
        if (mapUid == EntityUid.Invalid)
            return;

        if (!TryComp<MapGridComponent>(stationGridUid, out var stationMapGrid))
            return;

        SetupBiome(stationGridUid, ent.Comp);
        SetupPlanetGrid(stationGridUid, stationMapGrid, ent.Comp);
        SetupPlanetMap(mapUid, ent.Comp);
    }

    private void SetupBiome(EntityUid gridUid, ClassicStationBiomeComponent component)
    {
        var biome = EnsureComp<BiomeComponent>(gridUid);

        if (component.Seed is { } seed)
            _biome.SetSeed(gridUid, biome, seed);

        _biome.SetTemplate(gridUid, biome, _proto.Index(component.Biome));
    }

    private void SetupPlanetGrid(EntityUid gridUid, MapGridComponent grid, ClassicStationBiomeComponent component)
    {
        if (component.DisableGridSplitting)
        {
            grid.CanSplit = false;
            Dirty(gridUid, grid);
        }

        var gravity = EnsureComp<GravityComponent>(gridUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(gridUid, gravity);

        var roof = EnsureComp<RoofComponent>(gridUid);
        RemCompDeferred<ImplicitRoofComponent>(gridUid);
        SetupStationRoof(gridUid, grid, roof, component);

        EnsureComp<SunShadowComponent>(gridUid);
        EnsureComp<SunShadowCycleComponent>(gridUid);
    }

    private void SetupStationRoof(EntityUid gridUid, MapGridComponent grid, RoofComponent roof, ClassicStationBiomeComponent component)
    {
        if (!component.RoofStationTiles || component.StationRoofTiles.Count == 0)
            return;

        var tiles = _map.GetAllTilesEnumerator(gridUid, grid);
        while (tiles.MoveNext(out var tileRef))
        {
            if (tileRef.Value.Tile.IsEmpty)
                continue;

            var tileDef = _tile[tileRef.Value.Tile.TypeId];
            if (!component.StationRoofTiles.Contains(tileDef.ID))
                continue;

            _roof.SetRoof((gridUid, grid, roof), tileRef.Value.GridIndices, true);
        }
    }

    private void SetupPlanetMap(EntityUid mapUid, ClassicStationBiomeComponent component)
    {
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = component.MapLightColor;
        Dirty(mapUid, light);

        EnsureComp<LightCycleComponent>(mapUid);

        _atmosphere.SetMapAtmosphere(mapUid, false, PlanetAtmosphere);
    }

    private static GasMixture CreatePlanetAtmosphere()
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;

        return new GasMixture(moles, Atmospherics.T20C);
    }
}
