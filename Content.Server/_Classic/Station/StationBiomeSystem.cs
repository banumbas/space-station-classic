using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Parallax;
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
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly GasMixture PlanetAtmosphere = CreatePlanetAtmosphere();

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<ClassicStationBiomeComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<ClassicStationBiomeComponent> ent, ref StationPostInitEvent args)
    {
        TrySetupPlanet(ent, out _, out _, out _);
    }

    private bool TrySetupPlanet(
        Entity<ClassicStationBiomeComponent> station,
        out EntityUid stationGridUid,
        out MapGridComponent stationGrid,
        out EntityUid mapUid)
    {
        stationGridUid = EntityUid.Invalid;
        stationGrid = default!;
        mapUid = EntityUid.Invalid;

        var stationGridEntity = _station.GetLargestGrid(station.Owner);
        if (stationGridEntity == null)
            return false;

        stationGridUid = stationGridEntity.Value;
        if (!_xformQuery.TryComp(stationGridUid, out var stationGridXform))
            return false;

        mapUid = _map.GetMapOrInvalid(stationGridXform.MapID);
        if (mapUid == EntityUid.Invalid)
            return false;

        if (!_gridQuery.TryComp(stationGridUid, out var foundStationGrid))
            return false;

        stationGrid = foundStationGrid;
        SetupBiome(stationGridUid, station.Comp);
        SetupPlanetGrid(stationGridUid, stationGrid, station.Comp);
        SetupPlanetMap(mapUid, station.Comp);
        return true;
    }

    /// <summary>
    /// Generates persistent planetary terrain underneath the world-space bounds of the supplied grids.
    /// </summary>
    public bool TryGenerateBiomeGround(
        Entity<ClassicStationBiomeComponent?> station,
        MapId groundMapId,
        IReadOnlyList<EntityUid> sourceGrids)
    {
        if (!Resolve(station, ref station.Comp, false) ||
            !TrySetupPlanet((station.Owner, station.Comp), out var stationGridUid, out var stationGrid, out _) ||
            !_xformQuery.TryComp(stationGridUid, out var stationGridXform) ||
            stationGridXform.MapID != groundMapId ||
            !TryComp<BiomeComponent>(stationGridUid, out var biome))
        {
            return false;
        }

        var groundInvWorld = _transform.GetInvWorldMatrix(stationGridXform);
        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var sourceGridUid in sourceGrids)
        {
            if (sourceGridUid == EntityUid.Invalid ||
                !_gridQuery.TryComp(sourceGridUid, out var sourceGrid) ||
                !_xformQuery.TryComp(sourceGridUid, out var sourceXform))
            {
                continue;
            }

            var worldBounds = _transform.GetWorldMatrix(sourceXform).TransformBox(sourceGrid.LocalAABB);
            var groundLocalBounds = groundInvWorld.TransformBox(worldBounds);

            tiles.Clear();
            _biome.ReserveTiles(stationGridUid, groundLocalBounds, tiles, biome, stationGrid);
        }

        return true;
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

        EnsureComp<RoofComponent>(gridUid);
        RemCompDeferred<ImplicitRoofComponent>(gridUid);

        EnsureComp<SunShadowComponent>(gridUid);
        EnsureComp<SunShadowCycleComponent>(gridUid);
    }

    private void SetupPlanetMap(EntityUid mapUid, ClassicStationBiomeComponent component)
    {
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = component.MapLightColor;
        Dirty(mapUid, light);

        var cycle = EnsureComp<LightCycleComponent>(mapUid);
        if (cycle.OriginalColor == Color.Transparent)
            cycle.OriginalColor = light.AmbientLightColor;
        cycle.InitialOffset = false;
        Dirty(mapUid, cycle);

        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = "Dirt";
        Dirty(mapUid, parallax);

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
