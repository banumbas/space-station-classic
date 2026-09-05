using Content.Server._Classic.ZCollapse;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Station.Components;
using Content.Shared._Classic.Station.Components;
using Content.Shared._Classic.ZLevels.Roof;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Classic.Station;

/// <summary>
/// Configures the surface and data-driven underground terrain grids for Classic stations.
/// </summary>
public sealed partial class ClassicStationBiomeSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ClassicZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The Z network is loaded synchronously. Running afterwards lets terrain be attached once,
        // without a retrying Update loop or map-wide scans every tick.
        SubscribeLocalEvent<ClassicStationBiomeComponent, StationPostInitEvent>(OnStationPostInit,
            after: [typeof(ClassicZLevelsSystem)]);
    }

    private void OnStationPostInit(Entity<ClassicStationBiomeComponent> ent, ref StationPostInitEvent args)
    {
        var surfaceGridUid = _station.GetLargestGrid(args.Station.Owner);
        if (surfaceGridUid == null ||
            !TryComp<MapGridComponent>(surfaceGridUid.Value, out var surfaceGrid) ||
            Transform(surfaceGridUid.Value).MapUid is not { } surfaceMapUid)
        {
            return;
        }

        var seed = ent.Comp.Seed ?? _random.Next();

        SetupBiome(surfaceGridUid.Value, ent.Comp.Biome, seed);
        SetupTerrainGrid(surfaceGridUid.Value, surfaceGrid, ent.Comp.DisableGridSplitting, sunlight: true);
        SetupSurfaceMap(surfaceMapUid, ent.Comp);
        EnsureComp<ClassicGridStabilityComponent>(surfaceGridUid.Value);

        if (!_zLevels.TryGetMapNetwork(surfaceMapUid, out var network))
            return;

        SetupUpperConstructionLevel(surfaceMapUid, ent.Comp);

        ClassicZMapNetworkComponent? networkComponent = network.Comp;
        var configuredDepths = new HashSet<int>();
        foreach (var level in ent.Comp.UndergroundLevels)
        {
            if (level.Depth >= 0 || !configuredDepths.Add(level.Depth))
            {
                Log.Error($"Invalid or duplicate Classic underground depth {level.Depth} on {ToPrettyString(ent)}.");
                continue;
            }

            if (!_zLevels.TryGetMapAtDepth((network.Owner, networkComponent), level.Depth, out var mapUid) ||
                !TryGetTerrainGrid(mapUid, out var grid))
            {
                Log.Error($"Classic underground map/grid at depth {level.Depth} was not loaded.");
                continue;
            }

            SetupUndergroundLevel(mapUid, grid, level, seed, ent.Comp.DisableGridSplitting);
        }
    }

    /// <summary>
    /// Generates persistent planetary terrain underneath the world-space bounds of the supplied grids.
    /// </summary>
    public bool TryGenerateBiomeGround(
        Entity<ClassicStationBiomeComponent?> station,
        MapId groundMapId,
        IReadOnlyList<EntityUid> sourceGrids)
    {
        if (!Resolve(station, ref station.Comp, false))
            return false;

        var stationGridUid = _station.GetLargestGrid(station.Owner);
        if (stationGridUid == null ||
            !TryComp<MapGridComponent>(stationGridUid.Value, out var stationGrid) ||
            !TryComp<TransformComponent>(stationGridUid.Value, out var stationGridXform) ||
            stationGridXform.MapID != groundMapId ||
            !TryComp<BiomeComponent>(stationGridUid.Value, out var biome))
        {
            return false;
        }

        var groundInvWorld = _transform.GetInvWorldMatrix(stationGridXform);
        var tiles = new List<(Vector2i Index, Tile Tile)>();

        foreach (var sourceGridUid in sourceGrids)
        {
            if (sourceGridUid == EntityUid.Invalid ||
                !TryComp<MapGridComponent>(sourceGridUid, out var sourceGrid) ||
                !TryComp<TransformComponent>(sourceGridUid, out var sourceXform))
            {
                continue;
            }

            var worldBounds = _transform.GetWorldMatrix(sourceXform).TransformBox(sourceGrid.LocalAABB);
            var groundLocalBounds = groundInvWorld.TransformBox(worldBounds);

            tiles.Clear();
            _biome.ReserveTiles(stationGridUid.Value, groundLocalBounds, tiles, biome, stationGrid);
        }

        return true;
    }

    private void SetupUpperConstructionLevel(EntityUid surfaceMapUid, ClassicStationBiomeComponent component)
    {
        if (!_zLevels.TryMapUp(surfaceMapUid, out var upperMap) ||
            !TryGetTerrainGrid(upperMap.Owner, out var upperGrid))
        {
            return;
        }

        SetupTerrainGrid(upperGrid.Owner, upperGrid.Comp, component.DisableGridSplitting, sunlight: true);
        SetupSurfaceMap(upperMap.Owner, component);
        EnsureComp<ClassicGridStabilityComponent>(upperGrid.Owner);
    }

    private void SetupUndergroundLevel(
        EntityUid mapUid,
        Entity<MapGridComponent> grid,
        ClassicUndergroundLevelData level,
        int seed,
        bool disableGridSplitting)
    {
        var marker = EnsureComp<ClassicUndergroundBiomeComponent>(grid.Owner);
        EnsureComp<StationAuxiliaryGridComponent>(grid.Owner);
        marker.Depth = level.Depth;
        marker.LoadEntities = level.LoadEntities;
        marker.LoadDecals = level.LoadDecals;

        // Natural terrain is deliberately outside the construction-collapse network.
        RemComp<ClassicGridStabilityComponent>(grid.Owner);

        SetupBiome(grid.Owner, level.Biome, seed);
        SetupTerrainGrid(grid.Owner, grid.Comp, disableGridSplitting, sunlight: false);
        SetupUndergroundMap(mapUid, level);
    }

    private bool TryGetTerrainGrid(EntityUid mapUid, out Entity<MapGridComponent> grid)
    {
        if (TryComp<MapGridComponent>(mapUid, out var mapGrid))
        {
            grid = (mapUid, mapGrid);
            return true;
        }

        grid = default;
        if (!TryComp<MapComponent>(mapUid, out var map))
            return false;

        var largestChunkCount = -1;
        foreach (var candidate in _map.GetAllGrids(map.MapId))
        {
            if (candidate.Comp.ChunkCount <= largestChunkCount)
                continue;

            largestChunkCount = candidate.Comp.ChunkCount;
            grid = candidate;
        }

        return grid.Owner != EntityUid.Invalid;
    }

    private void SetupBiome(EntityUid gridUid, ProtoId<BiomeTemplatePrototype> template, int seed)
    {
        var biome = EnsureComp<BiomeComponent>(gridUid);
        _biome.SetSeed(gridUid, biome, seed, false);
        _biome.SetTemplate(gridUid, biome, _proto.Index(template));
    }

    private void SetupTerrainGrid(EntityUid gridUid, MapGridComponent grid, bool disableGridSplitting, bool sunlight)
    {
        if (disableGridSplitting && grid.CanSplit)
        {
            grid.CanSplit = false;
            Dirty(gridUid, grid);
        }

        var gravity = EnsureComp<GravityComponent>(gridUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(gridUid, gravity);

        EnsureComp<RoofComponent>(gridUid);
        EnsureComp<ClassicZLevelRoofComponent>(gridUid);
        RemCompDeferred<ImplicitRoofComponent>(gridUid);

        if (sunlight)
        {
            EnsureComp<SunShadowComponent>(gridUid);
            EnsureComp<SunShadowCycleComponent>(gridUid);
        }
        else
        {
            RemComp<SunShadowComponent>(gridUid);
            RemComp<SunShadowCycleComponent>(gridUid);
        }
    }

    private void SetupSurfaceMap(EntityUid mapUid, ClassicStationBiomeComponent component)
    {
        SetMapLight(mapUid, component.MapLightColor);

        var cycle = EnsureComp<LightCycleComponent>(mapUid);
        if (cycle.OriginalColor == Color.Transparent)
            cycle.OriginalColor = component.MapLightColor;
        cycle.InitialOffset = false;
        Dirty(mapUid, cycle);

        SetupMapEnvironment(mapUid, Atmospherics.T20C);
    }

    private void SetupUndergroundMap(EntityUid mapUid, ClassicUndergroundLevelData level)
    {
        RemComp<LightCycleComponent>(mapUid);
        SetMapLight(mapUid, level.MapLightColor);
        SetupMapEnvironment(mapUid, level.Temperature, level.Parallax);
    }

    private void SetMapLight(EntityUid mapUid, Color color)
    {
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = color;
        Dirty(mapUid, light);
    }

    private void SetupMapEnvironment(EntityUid mapUid, float temperature, string parallaxName = "Dirt")
    {
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = parallaxName;
        Dirty(mapUid, parallax);

        _atmosphere.SetMapAtmosphere(mapUid, false, CreatePlanetAtmosphere(temperature));
    }

    private static GasMixture CreatePlanetAtmosphere(float temperature)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        return new GasMixture(moles, temperature);
    }
}
