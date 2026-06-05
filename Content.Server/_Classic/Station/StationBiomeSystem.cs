using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Station.Components;
using Robust.Server.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._Classic.Station;

/// <summary>
/// Creates a station planet and merges the station grid into the planet grid so
/// tile/node systems see both as one connected grid.
/// </summary>
public sealed partial class ClassicStationBiomeSystem : EntitySystem
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GridFixtureSystem _gridFixture = default!;

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

        _biome.EnsurePlanet(mapUid, _proto.Index(ent.Comp.Biome), ent.Comp.Seed, mapLight: ent.Comp.MapLightColor);

        if (stationGridUid == mapUid || !ent.Comp.MergeStationGrid)
            return;

        if (!TryComp<MapGridComponent>(mapUid, out var planetGrid) ||
            !TryComp<MapGridComponent>(stationGridUid, out var oldStationGrid) ||
            !TryComp<StationDataComponent>(stationUid, out var stationData))
        {
            return;
        }

        var planetXform = Transform(mapUid);
        var mergeMatrix = Matrix3Helpers.CreateTransform(stationGridXform.LocalPosition, stationGridXform.LocalRotation);
        var stationName = MetaData(stationUid).EntityName;

        _station.AddMainGridToStation(stationUid, mapUid, planetGrid, stationData, stationName);
        _station.RemoveMainGridFromStation(stationUid, stationGridUid, oldStationGrid, stationData);

        _gridFixture.Merge(
            mapUid,
            stationGridUid,
            mergeMatrix,
            planetGrid,
            oldStationGrid,
            planetXform,
            stationGridXform);
    }
}
