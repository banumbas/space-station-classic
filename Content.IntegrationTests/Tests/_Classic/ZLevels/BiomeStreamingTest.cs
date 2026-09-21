using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Classic.Station;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.Parallax;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared._Classic.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Tag;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Classic.ZLevels;

// This regression fixture controls streaming budgets and inspects generated terrain directly.
#pragma warning disable RA0002

[TestFixture]
[EnsureCVar(Side.Server, typeof(ClassicCCVars), nameof(ClassicCCVars.AtmosEnabled), true)]
public sealed class BiomeStreamingTest : GameTest
{
    private static readonly ProtoId<BiomeTemplatePrototype> UndergroundBiome = "ClassicUndergroundDirtStone";

    [TestPrototypes]
    private const string MarkerPrototypes = @"
- type: biomeMarkerLayer
  id: ClassicStreamingMarkerNodeTest
  prototype: FloraRockSolid
  size: 8
  radius: 1
  maxCount: 5

- type: biomeTemplate
  id: ClassicStreamingTileOnlyTest
  layers:
    - !type:BiomeTileLayer
      threshold: -1.0
      tile: ClassicDirt

- type: entity
  id: ClassicStreamingPhysicalViewerTest
  components:
    - type: Physics
      bodyType: KinematicController
    - type: Fixtures
      fixtures:
        body:
          shape: !type:PhysShapeCircle
            radius: 0.35
          density: 1
          layer: [SmallMobLayer]
          mask: [SmallMobMask]
    - type: ClassicZPhysics
";

    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task BiomeRangeMatchesScaledPriorityPvsHalfSide()
    {
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetMaxUpdateRange, 32f);
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetPvsPriorityRange, 32f);
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid mapUid = default;

        try
        {
            await Server.WaitPost(() =>
            {
                mapUid = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(
                    mapUid,
                    Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest"),
                    42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(mapUid);
                streaming.BackgroundChunksPerTick = 64;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                map.InitializeMap(mapUid);

                var viewer = em.SpawnEntity(null, new EntityCoordinates(mapUid, new Vector2(4.5f, 4.5f)));
                em.EnsureComponent<EyeComponent>(viewer).PvsScale = 1.5f;
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(mapUid);
                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedChunks.Count, Is.EqualTo(49),
                        "A 32m PVS side scaled by 1.5 is a 24m radius (seven 8m chunks here), not the old 64m radius.");
                    Assert.That(streaming.PartialLoads, Is.Empty);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (mapUid != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(mapUid));
        }
    }

    [Test]
    public async Task RenderOnlyCameraCannotForceUrgentBiomeWalls()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var subscribers = em.System<ViewSubscriberSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;
        EntityUid camera = default;
        EntityUid safeMap = default;
        EntityUid player = default;

        try
        {
            await Server.WaitPost(() =>
            {
                safeMap = map.CreateMap();
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(terrain, Server.ProtoMan.Index(UndergroundBiome), 42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.WorkBudget = TimeSpan.Zero;
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                map.InitializeMap(terrain);

                player = em.SpawnEntity(null, new EntityCoordinates(safeMap, Vector2.Zero));
                Server.PlayerMan.SetAttachedEntity(session, player);
                camera = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(1000.5f, 1000.5f)));
                subscribers.AddViewSubscriber(camera, session);

                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(terrain);
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.ViewerCells, Is.Empty,
                        "A render-only camera must not enter the pre-physics safety lane.");
                    Assert.That(streaming.PriorityLoadedCells, Is.Empty);
                    Assert.That(streaming.PartialLoads, Is.Empty,
                        "A zero background budget must prevent a camera from spawning even one wall.");
                    Assert.That(biome.LoadedEntities, Is.Empty);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (camera != EntityUid.Invalid)
                    subscribers.RemoveViewSubscriber(camera, session);
                Server.PlayerMan.SetAttachedEntity(session, originalViewer);
            });
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
            if (safeMap != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(safeMap));
        }
    }

    [Test]
    public async Task PhysicalViewerUsesSweptFixtureFootprintInsteadOfFixedSquare()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var physics = em.System<SharedPhysicsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(
                    terrain,
                    Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest"),
                    42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = 0;
                streaming.WorkBudget = TimeSpan.Zero;
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.ViewerCells, Is.EquivalentTo(new[] { new Vector2i(4, 4) }),
                        "A stationary 0.35m body must not synchronously generate the old 3x3 wall square.");
                    Assert.That(BitOperations.PopCount(streaming.PriorityLoadedCells[Vector2i.Zero]), Is.EqualTo(1));
                });

                physics.SetLinearVelocity(
                    viewer,
                    new Vector2(4.5f, 0f),
                    body: em.GetComponent<PhysicsComponent>(viewer));
                biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.ViewerCells,
                        Is.EquivalentTo(new[] { new Vector2i(4, 4), new Vector2i(5, 4) }),
                        "The two-tick sweep must generate only the current and forward cells.");
                    Assert.That(BitOperations.PopCount(streaming.PriorityLoadedCells[Vector2i.Zero]), Is.EqualTo(2));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task AdjacentLandingCellsRequireReachableZDirection()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var above = map.CreateMap(runMapInit: false);
                var current = map.CreateMap(runMapInit: false);
                var below = map.CreateMap(runMapInit: false);
                var tileOnly = Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest");
                foreach (var terrain in new[] { above, current, below })
                {
                    biomes.EnsurePlanet(terrain, tileOnly, 42);
                    var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                    streaming.BackgroundChunksPerTick = 0;
                    streaming.WorkBudget = TimeSpan.Zero;
                    streaming.UnloadDelay = TimeSpan.FromHours(1);
                }

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                Assert.That(zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [above] = 1,
                    [current] = 0,
                    [below] = -1,
                }), Is.True);
                map.InitializeMap(above);
                map.InitializeMap(current);
                map.InitializeMap(below);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(current, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var aboveStreaming = em.GetComponent<ClassicBiomeStreamingComponent>(above);
                var belowStreaming = em.GetComponent<ClassicBiomeStreamingComponent>(below);
                Assert.Multiple(() =>
                {
                    Assert.That(aboveStreaming.PartialLoads, Is.Empty,
                        "A grounded body must not prepare the level above it.");
                    Assert.That(belowStreaming.PartialLoads, Is.Empty,
                        "A proven solid floor must not prepare the level below it.");
                });

                var zPhysics = em.GetComponent<ClassicZPhysicsComponent>(viewer);
                zPhysics.Velocity = -1f;
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(belowStreaming.PartialLoads, Does.ContainKey(Vector2i.Zero));
                    Assert.That(BitOperations.PopCount(belowStreaming.PriorityLoadedCells[Vector2i.Zero]),
                        Is.EqualTo(1));
                    Assert.That(aboveStreaming.PartialLoads, Is.Empty);
                });

                zPhysics.Velocity = 1f;
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(aboveStreaming.PartialLoads, Does.ContainKey(Vector2i.Zero));
                    Assert.That(BitOperations.PopCount(aboveStreaming.PriorityLoadedCells[Vector2i.Zero]),
                        Is.EqualTo(1));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task OpaqueCurrentLevelKeepsHiddenLowerBiomeAtLandingChunk()
    {
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetMaxUpdateRange, 32f);
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetPvsPriorityRange, 32f);
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var current = map.CreateMap(runMapInit: false);
                var lower = map.CreateMap(runMapInit: false);
                var tileOnly = Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest");
                biomes.EnsurePlanet(current, tileOnly, 24);
                biomes.EnsurePlanet(lower, tileOnly, 42);
                foreach (var terrain in new[] { current, lower })
                {
                    var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                    streaming.BackgroundChunksPerTick = 64;
                    streaming.BackgroundCellsPerSlice = 64;
                    streaming.WorkBudget = TimeSpan.FromSeconds(1);
                    streaming.UnloadDelay = TimeSpan.FromHours(1);
                }

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [current] = 0,
                    [lower] = -1,
                });
                map.InitializeMap(current);
                map.InitializeMap(lower);

                // Exercise an exact PVS/chunk boundary: cache padding must not interpret an
                // unmaterialized neighbouring cache chunk as an opening.
                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(current, Vector2.Zero));
                Server.PlayerMan.SetAttachedEntity(session, viewer);

                // A predicted solid current tile proves that this body cannot fall. No adjacent
                // terrain should be touched while the current range becomes fully opaque.
                biomes.Update(0);
                biomes.Update(0);
                for (var i = 0; i < 8; i++)
                    biomes.Update(0);
                var currentBiome = em.GetComponent<BiomeComponent>(current);
                var lowerBiome = em.GetComponent<BiomeComponent>(lower);
                var lowerStreaming = em.GetComponent<ClassicBiomeStreamingComponent>(lower);
                Assert.Multiple(() =>
                {
                    Assert.That(lowerBiome.LoadedChunks, Is.Empty,
                        "A solid current floor must not activate the hidden lower biome.");
                    Assert.That(lowerStreaming.PartialLoads, Is.Empty,
                        "Horizontal movement over solid terrain must not create lower-Z partial chunks.");
                    Assert.That(lowerStreaming.PriorityLoadedCells, Is.Empty);
                });

                var currentGrid = em.GetComponent<MapGridComponent>(current);
                Assert.That(map.TryGetTileRef(current, currentGrid, Vector2i.Zero, out var opaqueTile), Is.True);

                // Two writes may share LastTileModifiedTick. Exact event invalidation must see
                // that the opening was closed again instead of exposing the lower viewport.
                map.SetTile(current, currentGrid, Vector2i.Zero, Tile.Empty);
                map.SetTile(current, currentGrid, Vector2i.Zero, opaqueTile.Tile);
                biomes.Update(0);
                Assert.That(lowerBiome.LoadedChunks, Is.Empty);

                // A real opening invalidates the cache. The opening is a visibility gate, not a
                // generation clip: once the lower eye is visible its complete PVS range must be
                // streamed instead of leaving a single biome chunk surrounded by void.
                var currentStreaming = em.GetComponent<ClassicBiomeStreamingComponent>(current);
                currentStreaming.WorkBudget = TimeSpan.FromTicks(1);
                map.SetTile(current, currentGrid, Vector2i.Zero, Tile.Empty);
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(lowerBiome.LoadedChunks, Is.Empty,
                        "An incomplete opening snapshot must keep the hidden lower viewport conservative.");
                    Assert.That(lowerStreaming.PartialLoads, Does.ContainKey(Vector2i.Zero),
                        "A real opening must synchronously prepare its exact fall destination.");
                    Assert.That(BitOperations.PopCount(lowerStreaming.PriorityLoadedCells[Vector2i.Zero]),
                        Is.EqualTo(1),
                        "Opening safety must stay at one cell until the lower viewport is proven visible.");
                    Assert.That(currentStreaming.OpeningScans.Values.Any(scan =>
                            scan.Phase != ClassicBiomeOpeningScanPhase.Complete),
                        Is.True,
                        "A full PVS opening snapshot must be resumed instead of scanned atomically.");
                });

                const int expectedLowerChunks = 25;
                for (var i = 0; i < 128 && lowerBiome.LoadedChunks.Count != expectedLowerChunks; i++)
                    biomes.Update(0);

                var currentTileIsEmpty = map.TryGetTileRef(
                    current,
                    currentGrid,
                    Vector2i.Zero,
                    out var currentTile) && currentTile.Tile.IsEmpty;
                Assert.That(lowerBiome.LoadedChunks.Count, Is.EqualTo(expectedLowerChunks),
                    $"current_chunks={currentBiome.LoadedChunks.Count} current_partial={currentStreaming.PartialLoads.Count} " +
                    $"current_tile_empty={currentTileIsEmpty} lower_partial={lowerStreaming.PartialLoads.Count} " +
                    $"opening_scans=[{string.Join(';', currentStreaming.OpeningScans.Values.Select(scan => $"{scan.Phase}:{scan.NextChunk}/{scan.NextTile}:{scan.HasOpening}"))}]");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task OpeningScansOnlyResetForIntersectingTileChanges()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(
                    terrain,
                    Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest"),
                    42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                map.InitializeMap(terrain);

                var nearFirst = Vector2i.Zero;
                var nearLast = new Vector2i(7, 7);
                var nearKey = new ClassicBiomeOpeningScanKey(
                    Vector2i.Zero,
                    Vector2i.Zero,
                    nearFirst,
                    nearLast);
                var nearScan = new ClassicBiomeOpeningScanState(
                    Vector2i.Zero,
                    Vector2i.Zero,
                    nearFirst,
                    nearLast,
                    0,
                    1,
                    TimeSpan.Zero);
                var farFirst = new Vector2i(80, 80);
                var farLast = new Vector2i(87, 87);
                var farKey = new ClassicBiomeOpeningScanKey(
                    new Vector2i(10, 10),
                    new Vector2i(10, 10),
                    farFirst,
                    farLast);
                var farScan = new ClassicBiomeOpeningScanState(
                    new Vector2i(10, 10),
                    new Vector2i(10, 10),
                    farFirst,
                    farLast,
                    0,
                    1,
                    TimeSpan.Zero);
                streaming.OpeningScans.Add(nearKey, nearScan);
                streaming.OpeningScans.Add(farKey, farScan);

                var grid = em.GetComponent<MapGridComponent>(terrain);
                var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                map.SetTile(terrain, grid, new Vector2i(1, 1), plating);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.OpeningScans, Does.Not.ContainKey(nearKey));
                    Assert.That(streaming.OpeningScans, Does.ContainKey(farKey));
                    Assert.That(farScan.Revision, Is.GreaterThan(0));
                });

                map.SetTile(terrain, grid, new Vector2i(81, 81), plating);
                Assert.That(streaming.OpeningScans, Does.Not.ContainKey(farKey));
            });
        }
        finally
        {
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task MissingOpeningChunkDoesNotConsumeForcedOpeningProgress()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(
                    terrain,
                    Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest"),
                    42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.WorkBudget = TimeSpan.Zero;
                streaming.OpeningCellsPerSlice = 1;
                map.InitializeMap(terrain);

                var biome = em.GetComponent<BiomeComponent>(terrain);
                var grid = em.GetComponent<MapGridComponent>(terrain);
                biome.LoadedChunks.Add(Vector2i.Zero);

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(BiomeSystem).GetMethod("BeginClassicStreamingBudget", flags)!.Invoke(biomes, null);
                var method = typeof(BiomeSystem).GetMethod("TryGetClassicOpeningWorldBounds", flags)!;
                var missingArgs = new object[]
                {
                    terrain,
                    biome,
                    grid,
                    new Box2(80f, 80f, 81f, 81f),
                    default(Box2),
                };
                Assert.That(method.Invoke(biomes, missingArgs), Is.False);

                var readyArgs = new object[]
                {
                    terrain,
                    biome,
                    grid,
                    new Box2(0f, 0f, 1f, 1f),
                    default(Box2),
                };
                Assert.That(method.Invoke(biomes, readyArgs), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.OpeningScans.Count, Is.EqualTo(2));
                    Assert.That(streaming.OpeningScans.Values.Single(scan =>
                            scan.RequiredFirstChunk == new Vector2i(10, 10)).Phase,
                        Is.EqualTo(ClassicBiomeOpeningScanPhase.WaitingForChunks));
                    Assert.That(streaming.OpeningScans.Values.Single(scan =>
                            scan.RequiredFirstChunk == Vector2i.Zero).Phase,
                        Is.EqualTo(ClassicBiomeOpeningScanPhase.Scanning));
                });
            });
        }
        finally
        {
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task OpeningScanCacheCapsCurrentGeneration()
    {
        var biomes = Server.EntMan.System<BiomeSystem>();

        await Server.WaitPost(() =>
        {
            var streaming = new ClassicBiomeStreamingComponent
            {
                MaxOpeningCacheEntries = 1,
                OpeningScanGeneration = 7,
            };
            var first = Vector2i.Zero;
            var second = new Vector2i(1, 0);
            streaming.OpeningScans.Add(
                new ClassicBiomeOpeningScanKey(first, first, first, first),
                new ClassicBiomeOpeningScanState(first, first, first, first, 0, 7, TimeSpan.Zero));
            streaming.OpeningScans.Add(
                new ClassicBiomeOpeningScanKey(second, second, second, second),
                new ClassicBiomeOpeningScanState(second, second, second, second, 0, 7, TimeSpan.Zero));

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BiomeSystem).GetMethod("PruneClassicOpeningScans", flags)!.Invoke(biomes, new object[] { streaming });

            Assert.That(streaming.OpeningScans.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TransformedSeparateBiomeGridsStreamInTheirOwnLocalCoordinates()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var upperMap = map.CreateMap(runMapInit: false);
                var lowerMap = map.CreateMap(runMapInit: false);
                var upperGrid = map.CreateGridEntity(upperMap);
                var lowerGrid = map.CreateGridEntity(lowerMap);
                upperGrid.Comp.CanSplit = false;
                lowerGrid.Comp.CanSplit = false;

                // The same world-space point belongs to unrelated local tiles on the two biome
                // grids. Distinct rotations also make an upper-local-as-lower-local shortcut fail.
                var upperRotation = Angle.FromDegrees(90);
                var lowerRotation = Angle.FromDegrees(-90);
                transform.SetWorldPositionRotation(upperGrid, new Vector2(40f, -20f), upperRotation);
                var upperViewerTile = new Vector2i(12, 20);
                var worldViewer = map.GridTileToWorldPos(upperGrid, upperGrid.Comp, upperViewerTile);
                var lowerViewerTile = new Vector2i(34, 6);
                var lowerViewerCenter = (Vector2) lowerViewerTile + new Vector2(0.5f);
                transform.SetWorldPositionRotation(
                    lowerGrid,
                    worldViewer - lowerRotation.RotateVec(lowerViewerCenter),
                    lowerRotation);

                var tileOnly = Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest");
                var upperBiome = em.EnsureComponent<BiomeComponent>(upperGrid);
                biomes.SetSeed(upperGrid, upperBiome, 24, false);
                biomes.SetTemplate(upperGrid, upperBiome, tileOnly, false);
                var lowerBiome = em.EnsureComponent<BiomeComponent>(lowerGrid);
                biomes.SetSeed(lowerGrid, lowerBiome, 42, false);
                biomes.SetTemplate(lowerGrid, lowerBiome, tileOnly, false);
                lowerBiome.MarkerLayers.Add("ClassicStreamingMarkerNodeTest");

                var upperStreaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(upperGrid);
                var lowerStreaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(lowerGrid);
                foreach (var streaming in new[] { upperStreaming, lowerStreaming })
                {
                    streaming.BackgroundChunksPerTick = 64;
                    streaming.BackgroundCellsPerSlice = 64;
                    streaming.WorkBudget = TimeSpan.FromSeconds(1);
                    streaming.UnloadDelay = TimeSpan.FromHours(1);
                }
                lowerStreaming.MarkerAreaCellsPerSlice = 1;

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                Assert.That(zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [upperMap] = 0,
                    [lowerMap] = -1,
                }), Is.True);
                map.InitializeMap(upperMap);
                map.InitializeMap(lowerMap);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    map.GridTileToLocal(upperGrid, upperGrid.Comp, upperViewerTile));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                var lowerMapId = em.GetComponent<MapComponent>(lowerMap).MapId;
                var mappedLowerTile = map.TileIndicesFor(
                    lowerGrid,
                    lowerGrid.Comp,
                    new MapCoordinates(worldViewer, lowerMapId));
                Assert.That(mappedLowerTile, Is.EqualTo(lowerViewerTile),
                    "The regression setup must place the two unrelated local tiles at one world point.");

                // The first pass fills the upper range; the second can prove it is opaque. Only
                // the exact adjacent-Z landing chunk is then active on the lower biome grid.
                biomes.Update(0);
                biomes.Update(0);

                var upperViewerChunk = new Vector2i(8, 16);
                var lowerViewerChunk = new Vector2i(32, 0);
                Assert.Multiple(() =>
                {
                    Assert.That(upperStreaming.ViewerCells, Is.EquivalentTo(new[] { upperViewerTile }),
                        "Physical viewer safety cells must be upper-grid-local, not world coordinates.");
                    Assert.That(upperStreaming.ViewerChunks, Is.EquivalentTo(new[] { upperViewerChunk }));
                    Assert.That(lowerStreaming.ViewerCells, Is.Empty);
                    Assert.That(lowerStreaming.ViewerChunks, Is.Empty);
                    Assert.That(lowerBiome.LoadedChunks, Is.Empty,
                        "An opaque upper biome must not generate hidden lower terrain.");
                    Assert.That(lowerStreaming.MarkerAreaScan, Is.Null,
                        "A landing-only request must not request a render-only marker area.");
                });

                Assert.That(map.TryGetTileRef(
                    upperGrid.Owner,
                    upperGrid.Comp,
                    upperViewerTile,
                    out var upperTile), Is.True);
                Assert.That(upperTile.Tile.IsEmpty, Is.False);
                map.SetTile(upperGrid, upperViewerTile, Tile.Empty);

                // The opening proves visibility; generation still covers the complete lower-eye
                // PVS range in the lower grid's own transformed local coordinates.
                var expectedLowerChunks = new HashSet<Vector2i>();
                for (var x = 16; x <= 48; x += 8)
                for (var y = -16; y <= 16; y += 8)
                    expectedLowerChunks.Add(new Vector2i(x, y));
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(lowerBiome.LoadedChunks, Is.Empty,
                        "An incomplete transformed opening snapshot must not expose lower terrain.");
                    Assert.That(upperStreaming.OpeningScans.Values.Any(scan =>
                            scan.Phase != ClassicBiomeOpeningScanPhase.Complete),
                        Is.True,
                        "The transformed PVS opening snapshot must be resumed over bounded slices.");
                });

                for (var i = 0;
                     i < 64 && !lowerBiome.LoadedChunks.ToHashSet().SetEquals(expectedLowerChunks);
                     i++)
                {
                    biomes.Update(0);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(lowerBiome.LoadedChunks, Is.EquivalentTo(expectedLowerChunks),
                        "The lower eye must stream its complete PVS in lower-grid-local coordinates.");
                    Assert.That(lowerStreaming.ViewerCells, Is.EquivalentTo(new[] { lowerViewerTile }));
                    Assert.That(lowerStreaming.ViewerChunks, Is.Empty,
                        "A render-only lower eye must not enter the physical viewer lane.");
                    Assert.That(lowerStreaming.MarkerAreaScan, Is.Not.Null,
                        "The visible lower local bounds must request marker work.");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task RoofBatchPreservesTileMasksAndEntityColors()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var roofSystem = em.System<SharedRoofSystem>();
        EntityUid mapUid = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                mapUid = map.CreateMap();
                var grid = map.CreateGridEntity(mapUid);
                grid.Comp.CanSplit = false;
                var plating = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                map.SetTile(grid, Vector2i.Zero, plating);
                map.SetTile(grid, new Vector2i(1, 0), plating);
                var roof = em.EnsureComponent<RoofComponent>(grid);
                var roofEntity = (grid.Owner, grid.Comp, roof);
                var bounds = new Box2(-1, -1, 3, 2);
                roofSystem.SetRoof(roofEntity, Vector2i.Zero, true);
                Assert.That(roofSystem.HasRoofEntities(grid, bounds), Is.False);
                Assert.That(roofSystem.GetColor(roofEntity, Vector2i.Zero, false), Is.EqualTo(roof.Color));
                Assert.That(roofSystem.GetColor(roofEntity, new Vector2i(1, 0), false), Is.Null);

                var wall = em.SpawnEntity("WallSolid", map.GridTileToLocal(grid, grid.Comp, new Vector2i(1, 0)));
                var entityRoof = em.GetComponent<IsRoofComponent>(wall);
                entityRoof.Color = Color.Red;
                var checkEntities = roofSystem.HasRoofEntities(grid, bounds);
                Assert.That(checkEntities, Is.True);
                Assert.That(roofSystem.GetColor(roofEntity, new Vector2i(1, 0), checkEntities), Is.EqualTo(Color.Red));

                entityRoof.Enabled = false;
                checkEntities = roofSystem.HasRoofEntities(grid, bounds);
                Assert.That(checkEntities, Is.False);
                Assert.That(roofSystem.GetColor(roofEntity, new Vector2i(1, 0), checkEntities), Is.Null);
                Assert.That(roofSystem.GetColor(roofEntity, Vector2i.Zero, checkEntities), Is.EqualTo(roof.Color));
            });
        }
        finally
        {
            if (mapUid != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(mapUid));
        }
    }

    [Test]
    public async Task BackgroundChunkLoadsAreSlicedAndViewerPromotionIsImmediate()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var underground = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(underground, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(underground);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(underground);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 8;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                streaming.UnloadDelay = TimeSpan.FromHours(1);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [underground] = -1,
                });
                if (!map.IsInitialized(underground))
                    map.InitializeMap(underground);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(underground, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                var biome = em.GetComponent<BiomeComponent>(underground);

                biomes.Update(0);
                var firstPartial = streaming.PartialLoads.Single();
                var partialChunk = firstPartial.Key;
                Assert.Multiple(() =>
                {
                    Assert.That(firstPartial.Value, Is.EqualTo(4));
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(partialChunk),
                        "A partial chunk must not be exposed to the unload path as complete.");
                    Assert.That(biome.LoadedEntities[partialChunk].Count, Is.EqualTo(5),
                        "The physical cell ahead of the sequential cursor must be the only urgent addition.");
                    Assert.That(CountNonEmptyTiles(partialChunk), Is.EqualTo(9));
                    Assert.That(BitOperations.PopCount(streaming.PriorityLoadedCells[partialChunk]), Is.EqualTo(1));
                });

                for (var i = 0; i < 20 && !biome.LoadedChunks.Contains(partialChunk); i++)
                {
                    var before = biome.LoadedEntities[partialChunk].Count;
                    biomes.Update(0);
                    var after = biome.LoadedEntities[partialChunk].Count;
                    Assert.That(after - before, Is.InRange(0, 4));
                }

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialLoads.ContainsKey(partialChunk), Is.False);
                    Assert.That(streaming.PriorityLoadedCells.ContainsKey(partialChunk), Is.False);
                    Assert.That(biome.LoadedChunks, Does.Contain(partialChunk));
                    Assert.That(biome.LoadedEntities[partialChunk].Count, Is.EqualTo(64));
                    Assert.That(CountNonEmptyTiles(partialChunk), Is.EqualTo(64));
                });

                // Moving away from an unfinished chunk must cancel it incrementally. Completing
                // 56 invisible wall cells merely to delete all 64 again was a movement-amplified
                // source of TTA spikes.
                streaming.UnloadDelay = TimeSpan.Zero;
                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                biomes.Update(0);
                var inactivePartial = streaming.PartialLoads.Single();
                var inactiveChunk = inactivePartial.Key;
                Assert.That(inactivePartial.Value, Is.EqualTo(4));
                streaming.BackgroundChunksPerTick = 0;
                Server.PlayerMan.SetAttachedEntity(session, originalViewer);
                var largestCursor = inactivePartial.Value;
                for (var i = 0; i < 192; i++)
                {
                    biomes.Update(0);
                    if (streaming.PartialLoads.TryGetValue(inactiveChunk, out var cursor))
                        largestCursor = Math.Max(largestCursor, cursor);
                    if (!streaming.PartialLoads.ContainsKey(inactiveChunk) &&
                        !streaming.PartialUnloads.ContainsKey(inactiveChunk) &&
                        !biome.LoadedEntities.ContainsKey(inactiveChunk))
                    {
                        break;
                    }
                }

                Assert.Multiple(() =>
                {
                    Assert.That(largestCursor, Is.EqualTo(4),
                        "An inactive partial chunk must never be completed in the background.");
                    Assert.That(streaming.PartialLoads.ContainsKey(inactiveChunk), Is.False);
                    Assert.That(streaming.PartialUnloads.ContainsKey(inactiveChunk), Is.False);
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(inactiveChunk));
                    Assert.That(biome.LoadedEntities.ContainsKey(inactiveChunk), Is.False);
                    Assert.That(CountNonEmptyTiles(inactiveChunk), Is.Zero);
                    Assert.That(streaming.PendingUnloads.ContainsKey(inactiveChunk), Is.False);
                    Assert.That(biome.ModifiedTiles.ContainsKey(inactiveChunk), Is.False,
                        "Never-generated partial cells must not become permanent modifications during finalize.");
                });

                // A fully cancelled partial chunk must remain regenerable. The old all-64-cell
                // finalize path poisoned untouched cells as ModifiedTiles and left permanent
                // holes on this second visit.
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 8;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                transform.SetWorldPosition(
                    viewer,
                    new Vector2(inactiveChunk.X + 4.5f, inactiveChunk.Y + 4.5f));
                for (var i = 0; i < 32 && !biome.LoadedChunks.Contains(inactiveChunk); i++)
                    biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedChunks, Does.Contain(inactiveChunk));
                    Assert.That(biome.LoadedEntities[inactiveChunk].Count, Is.EqualTo(64));
                    Assert.That(CountNonEmptyTiles(inactiveChunk), Is.EqualTo(64));
                    Assert.That(biome.ModifiedTiles.ContainsKey(inactiveChunk), Is.False);
                });

                int CountNonEmptyTiles(Vector2i origin)
                {
                    var count = 0;
                    for (var x = 0; x < 8; x++)
                    for (var y = 0; y < 8; y++)
                    {
                        var indices = origin + new Vector2i(x, y);
                        if (map.TryGetTileRef(underground, grid, indices, out var tile) && !tile.Tile.IsEmpty)
                            count++;
                    }

                    return count;
                }
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task PartialUnloadRollsBackBeforeViewerCanObserveMissingWalls()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var underground = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(underground, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(underground);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(underground);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.BackgroundEntitySpawnsPerTick = 64;
                streaming.UnloadEntitiesPerSlice = 2;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [underground] = -1,
                });
                if (!map.IsInitialized(underground))
                    map.InitializeMap(underground);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(underground, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(underground);
                var origin = Vector2i.Zero;
                var originalEntities = biome.LoadedEntities[origin].Keys.ToHashSet();
                Assert.That(originalEntities.Count, Is.EqualTo(64));

                streaming.BackgroundChunksPerTick = 0;
                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(origin));
                    Assert.That(streaming.PartialUnloads.TryGetValue(origin, out var partial), Is.True);
                    Assert.That(partial!.Phase, Is.EqualTo(ClassicBiomePartialUnloadPhase.Entities));
                    Assert.That(partial.RemovedEntityCells.Count, Is.EqualTo(2));
                    Assert.That(biome.LoadedEntities[origin].Count, Is.EqualTo(62));
                });

                // Viewer promotion runs before optional streaming work and must synchronously
                // roll back the two missing walls, without rebuilding the other 62.
                transform.SetWorldPosition(viewer, new Vector2(4.5f, 4.5f));
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialUnloads.ContainsKey(origin), Is.False);
                    Assert.That(biome.LoadedChunks, Does.Contain(origin));
                    Assert.That(biome.LoadedEntities[origin].Count, Is.EqualTo(64));
                    Assert.That(biome.LoadedEntities[origin].Keys.Intersect(originalEntities).Count(), Is.EqualTo(62));
                });

                foreach (var tile in biome.LoadedEntities[origin].Values)
                {
                    var anchored = map.GetAnchoredEntities(underground, grid, tile);
                    var count = 0;
                    while (anchored.MoveNext(out _))
                        count++;
                    Assert.That(count, Is.EqualTo(1), $"Rollback duplicated the wall at {tile}.");
                }
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task ExpiredBudgetStillStartsAndFinishesPendingUnload()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(terrain, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(terrain);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 1;
                streaming.ViewerSafetyRadius = 0;
                streaming.UnloadEntitiesPerSlice = 1;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.WorkBudget = TimeSpan.Zero;
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(terrain);
                var origin = Vector2i.Zero;
                Assert.That(streaming.PartialLoads, Does.ContainKey(origin),
                    "The setup must leave bounded terrain work for the inactive-unload path.");

                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                for (var i = 0; i < 200; i++)
                {
                    biomes.Update(0);
                    if (!streaming.PartialLoads.ContainsKey(origin) &&
                        !streaming.PartialUnloads.ContainsKey(origin) &&
                        !streaming.PendingUnloads.ContainsKey(origin))
                    {
                        break;
                    }
                }

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PendingUnloads, Does.Not.ContainKey(origin),
                        "A mature pending unload must not starve behind an already-expired soft budget.");
                    Assert.That(streaming.PartialUnloads, Does.Not.ContainKey(origin));
                    Assert.That(streaming.PartialLoads, Does.Not.ContainKey(origin));
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(origin));
                    Assert.That(biome.LoadedEntities, Does.Not.ContainKey(origin));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task ReturningToCancellingPartialChunkRestoresItsCursorWithoutPromotion()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(terrain, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(terrain);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 8;
                streaming.BackgroundEntitySpawnsPerTick = 64;
                streaming.UnloadEntitiesPerSlice = 2;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(terrain);
                var origin = Vector2i.Zero;
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialLoads[origin], Is.EqualTo(8));
                    Assert.That(BitOperations.PopCount(streaming.PriorityLoadedCells[origin]), Is.EqualTo(9));
                    Assert.That(biome.LoadedEntities[origin].Count, Is.EqualTo(17));
                });

                streaming.BackgroundChunksPerTick = 0;
                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                biomes.Update(0);
                Assert.That(streaming.PartialUnloads.TryGetValue(origin, out var cancelling), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(cancelling!.WasPartial, Is.True);
                    Assert.That(cancelling.PartialCursor, Is.EqualTo(8));
                    Assert.That(cancelling.RemovedEntityCells.Count, Is.EqualTo(2));
                });

                // Re-enter at a different, never-generated part of the same chunk while two old
                // priority walls are gone. The new physical safety cells and the removed walls
                // are restored immediately, but the remainder stays bounded background work.
                streaming.WorkBudget = TimeSpan.Zero;
                transform.SetWorldPosition(viewer, new Vector2(1.5f, 6.5f));
                biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialUnloads.ContainsKey(origin), Is.False);
                    Assert.That(streaming.PartialLoads[origin], Is.EqualTo(8));
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(origin));
                    Assert.That(BitOperations.PopCount(streaming.PriorityLoadedCells[origin]), Is.EqualTo(15));
                    Assert.That(BitOperations.PopCount(streaming.MaterializedCells[origin]), Is.InRange(23, 28));
                    Assert.That(biome.LoadedEntities[origin].Count, Is.EqualTo(23));
                });

                var requested = new Vector2i(1, 6);
                var anchored = map.GetAnchoredEntities(terrain, grid, requested);
                Assert.That(anchored.MoveNext(out _), Is.True,
                    "A physical return must materialize a previously untouched predicted wall before physics.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task ExpiredZCacheContinuesPartialUnloadInsteadOfRestoringIt()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(terrain, Server.ProtoMan.Index(UndergroundBiome), 42);
                em.GetComponent<MapGridComponent>(terrain).CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.BackgroundEntitySpawnsPerTick = 64;
                streaming.UnloadEntitiesPerSlice = 2;
                streaming.UnloadEntitiesPerTick = 2;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.ZLevelCacheDuration = TimeSpan.Zero;
                streaming.MaxZLevelCachedChunks = 1;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                var cacheEye = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(terrain);
                var origin = Vector2i.Zero;
                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                biomes.Update(0);
                Assert.That(streaming.PartialUnloads.TryGetValue(origin, out var unloading), Is.True);
                Assert.That(unloading!.Phase, Is.EqualTo(ClassicBiomePartialUnloadPhase.Entities));
                var entitiesBeforeCache = biome.LoadedEntities[origin].Keys.ToHashSet();

                biomes.CacheClassicZLevel(cacheEye);
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.CachedZChunks, Does.ContainKey(origin));
                    Assert.That(unloading.Phase, Is.EqualTo(ClassicBiomePartialUnloadPhase.Restoring));
                });

                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.CachedZChunks, Does.Not.ContainKey(origin));
                    Assert.That(streaming.PartialUnloads.TryGetValue(origin, out var continuing), Is.True);
                    Assert.That(continuing!.Phase, Is.EqualTo(ClassicBiomePartialUnloadPhase.Entities));
                    Assert.That(biome.LoadedEntities[origin].Keys, Is.SubsetOf(entitiesBeforeCache));
                    Assert.That(biome.LoadedEntities[origin].Count, Is.LessThan(entitiesBeforeCache.Count));
                });

                for (var i = 0; i < 100 && streaming.PartialUnloads.ContainsKey(origin); i++)
                    biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialUnloads, Does.Not.ContainKey(origin));
                    Assert.That(streaming.PendingUnloads, Does.Not.ContainKey(origin));
                    Assert.That(biome.LoadedChunks, Does.Not.Contain(origin));
                    Assert.That(biome.LoadedEntities, Does.Not.ContainKey(origin));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task MarkerAreaScanIsBoundedDeterministicAndCancelsStaleRequests()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(
                    terrain,
                    Server.ProtoMan.Index<BiomeTemplatePrototype>("ClassicStreamingTileOnlyTest"),
                    42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = 0;
                streaming.MarkerAreaCellsPerSlice = 3;
                streaming.MarkerNodesPerSlice = 1;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                var biome = em.GetComponent<BiomeComponent>(terrain);
                biome.MarkerLayers.Add("ClassicStreamingMarkerNodeTest");
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(null, new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var firstScan = streaming.MarkerAreaScan;
                Assert.That(firstScan, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(firstScan!.NextCell, Is.EqualTo(3));
                    Assert.That(firstScan.RemainingTiles.Count, Is.EqualTo(3));
                    Assert.That(biome.LoadedMarkers.ContainsKey(firstScan.Layer), Is.False);
                });

                var layerProto = Server.ProtoMan.Index<BiomeMarkerLayerPrototype>(firstScan!.Layer);
                biomes.GetMarkerNodes(
                    terrain,
                    biome,
                    em.GetComponent<MapGridComponent>(terrain),
                    layerProto,
                    firstScan.Forced,
                    firstScan.Bounds,
                    firstScan.Count,
                    new Random(firstScan.MarkerSeed),
                    out var expected,
                    out _);

                biomes.Update(0);
                Assert.That(streaming.MarkerAreaScan!.NextCell, Is.EqualTo(6));

                // Removing the range request must discard the read-only snapshot instead of
                // publishing nodes for an area that is no longer observed.
                biome.MarkerLayers.Clear();
                biomes.Update(0);
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.MarkerAreaScan, Is.Null);
                    Assert.That(biome.LoadedMarkers.ContainsKey(firstScan.Layer), Is.False);
                    Assert.That(biome.PendingMarkers, Is.Empty);
                });

                // Restart with a different slice width. The completed node set must match the
                // legacy atomic algorithm because random draws happen only after row-major scan.
                biome.MarkerLayers.Add(firstScan.Layer);
                streaming.MarkerAreaCellsPerSlice = 64;
                biomes.Update(0);

                Assert.Multiple(() =>
                {
                    Assert.That(streaming.MarkerAreaScan, Is.Not.Null);
                    Assert.That(streaming.MarkerAreaScan!.Phase,
                        Is.EqualTo(ClassicBiomeMarkerAreaScanPhase.Selecting));
                    Assert.That(biome.LoadedMarkers.ContainsKey(firstScan.Layer), Is.False);
                    Assert.That(biome.PendingMarkers, Is.Empty);
                });

                var selectionTicks = 0;
                while (streaming.MarkerAreaScan != null && selectionTicks++ < 128)
                    biomes.Update(0);

                var actual = biome.PendingMarkers.Values
                    .Where(layers => layers.ContainsKey(firstScan.Layer))
                    .SelectMany(layers => layers[firstScan.Layer])
                    .ToHashSet();
                foreach (var modified in biome.ModifiedTiles.Values)
                foreach (var node in modified)
                {
                    if (expected.ContainsKey(node))
                        actual.Add(node);
                }
                Assert.Multiple(() =>
                {
                    Assert.That(selectionTicks, Is.LessThan(128),
                        "The bounded seeded selection did not converge.");
                    Assert.That(streaming.MarkerAreaScan, Is.Null);
                    Assert.That(biome.LoadedMarkers[firstScan.Layer], Does.Contain(firstScan.MarkerChunk));
                    Assert.That(actual, Is.EquivalentTo(expected.Keys));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task MarkerNodesOnLoadedBackgroundChunkRespectSharedEntityQuota()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var underground = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(underground, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(underground);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(underground);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.BackgroundEntitySpawnsPerTick = 64;
                streaming.MarkerNodesPerSlice = 2;
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                streaming.WorkBudget = TimeSpan.FromSeconds(1);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [underground] = -1,
                });
                if (!map.IsInitialized(underground))
                    map.InitializeMap(underground);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(underground, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(underground);
                var origin = Vector2i.Zero;
                var markerNodes = biome.LoadedEntities[origin].Values.Take(5).ToList();
                biome.PendingMarkers[origin] = new Dictionary<string, List<Vector2i>>
                {
                    ["ClassicStreamingMarkerNodeTest"] = markerNodes,
                };

                // Keep the old chunk in the same-level PVS range but make it non-urgent; adjacent
                // Z landing visibility is intentionally only one exact cell and no longer keeps
                // an unrelated underground chunk active through an opaque surface.
                streaming.BackgroundChunksPerTick = 0;
                streaming.BackgroundEntitySpawnsPerTick = 1;
                transform.SetCoordinates(viewer, new EntityCoordinates(underground, new Vector2(12.5f, 4.5f)));
                for (var i = 0; i < 16 && biome.PendingMarkers.ContainsKey(origin); i++)
                {
                    var nodesBefore = biome.PendingMarkers[origin]["ClassicStreamingMarkerNodeTest"].Count;
                    var entitiesBefore = biome.LoadedEntities[origin].Count;
                    biomes.Update(0);
                    var nodesAfter = biome.PendingMarkers.TryGetValue(origin, out var pending)
                        ? pending["ClassicStreamingMarkerNodeTest"].Count
                        : 0;
                    var entitiesAfter = biome.LoadedEntities[origin].Count;
                    Assert.Multiple(() =>
                    {
                        Assert.That(nodesBefore - nodesAfter, Is.InRange(0, 1));
                        Assert.That(entitiesBefore - entitiesAfter, Is.InRange(0, 1));
                    });
                }

                Assert.Multiple(() =>
                {
                    Assert.That(biome.PendingMarkers.ContainsKey(origin), Is.False);
                    Assert.That(biome.LoadedEntities[origin].Count, Is.EqualTo(59));
                    Assert.That(biome.ModifiedTiles[origin].Count, Is.EqualTo(5));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task BackgroundSlicesRotateBetweenDistantViewers()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var subscribers = em.System<ViewSubscriberSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;
        EntityUid auxiliary = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var underground = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(underground, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(underground);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(underground);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 8;
                streaming.UnloadDelay = TimeSpan.FromHours(1);
                streaming.WorkBudget = TimeSpan.FromSeconds(1);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [underground] = -1,
                });
                if (!map.IsInitialized(underground))
                    map.InitializeMap(underground);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(underground, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                auxiliary = em.SpawnEntity(null,
                    new EntityCoordinates(underground, new Vector2(1004.5f, 4.5f)));
                subscribers.AddViewSubscriber(auxiliary, session);

                biomes.Update(0);
                biomes.Update(0);

                Assert.That(streaming.PartialLoads.Count, Is.EqualTo(2),
                    "Each viewer must receive a background slice before either consumes a second one.");
                Assert.Multiple(() =>
                {
                    Assert.That(streaming.PartialLoads.Keys.Any(chunk => Math.Abs(chunk.X) <= 24), Is.True,
                        "The first viewer did not receive preload work.");
                    Assert.That(streaming.PartialLoads.Keys.Any(chunk => Math.Abs(chunk.X - 1000) <= 24), Is.True,
                        "The distant viewer was starved by coordinate tie-breaking.");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (auxiliary != EntityUid.Invalid)
                    subscribers.RemoveViewSubscriber(auxiliary, session);
                Server.PlayerMan.SetAttachedEntity(session, originalViewer);
            });
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task AdjacentUndergroundStreamingPreservesMiningDamageAndCustomComponents()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;
        var pristineUnloaded = false;
        var pristineRegenerated = false;
        var minedStayedEmpty = false;
        var damagePreserved = false;
        var componentPreserved = false;
        var tagPreserved = false;
        var gatherablePreserved = false;
        var loadedInitially = 0;
        var landingCellLoaded = false;
        var teleportedLandingCellLoaded = false;
        var unloadDiagnostics = string.Empty;

        try
        {
            await Server.WaitPost(() =>
            {
                var mapUid = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(mapUid, Server.ProtoMan.Index(UndergroundBiome), 42);
                var grid = em.GetComponent<MapGridComponent>(mapUid);
                grid.CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(mapUid);
                streaming.BackgroundChunksPerTick = 2;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.BackgroundEntitySpawnsPerTick = 64;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [mapUid] = -1,
                });
                if (!map.IsInitialized(mapUid))
                    map.InitializeMap(mapUid);
                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(surface, new Vector2(-4.5f, -4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                em.GetComponent<ClassicZPhysicsComponent>(viewer).Velocity = -1f;

                // A real downward transition request materializes exactly one adjacent cell; it
                // must not promote the rest of the hidden chunk even with ample spare budget.
                biomes.Update(0);
                var biome = em.GetComponent<BiomeComponent>(mapUid);
                var origin = new Vector2i(-8, -8);
                landingCellLoaded = biome.LoadedChunks.Count == 0 &&
                    biome.LoadedEntities.TryGetValue(origin, out var adjacentLoaded) &&
                    adjacentLoaded.Count == 1;

                // Entering the level makes the chunk render-active; only then may the bounded
                // background lane finish all of its cells for mining/persistence checks below.
                transform.SetCoordinates(viewer,
                    new EntityCoordinates(mapUid, new Vector2(-4.5f, -4.5f)));
                em.GetComponent<ClassicZPhysicsComponent>(viewer).Velocity = 0f;
                biomes.Update(0);
                loadedInitially = biome.LoadedChunks.Count;
                var loaded = biome.LoadedEntities[origin];
                var rocks = loaded!.Take(6).ToArray();
                var pristine = rocks[0];
                var mined = rocks[1];
                var damaged = rocks[2];
                var customized = rocks[3];
                var tagged = rocks[4];
                var gatherableCustomized = rocks[5];

                em.DeleteEntity(mined.Key);
                em.System<DamageableSystem>().TryChangeDamage(damaged.Key,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, ignoreResistances: true);
                em.EnsureComponent<PointLightComponent>(customized.Key);
                em.System<TagSystem>().AddTag(tagged.Key, new ProtoId<TagPrototype>("Pickaxe"));
                em.GetComponent<Content.Server.Gatherable.Components.GatherableComponent>(gatherableCustomized.Key)
                    .GatherOffset += 1f;

                // Leave the chunk and let its unload deadline pass. No background loading is
                // needed for this part, so the return trip exercises exactly the original chunk.
                // Z-eyes still point at the old location until their periodic update; the main
                // physical viewer must already use the current owner position.
                streaming.BackgroundChunksPerTick = 0;
                transform.SetWorldPosition(viewer, new Vector2(1004.5f, 1004.5f));
                biomes.Update(0);
                teleportedLandingCellLoaded = biome.LoadedEntities.TryGetValue(new Vector2i(1000, 1000), out var destination) &&
                    destination.Count == 64;
                var unloadStates = new List<string>();
                for (var i = 0; i < 80; i++)
                {
                    biomes.Update(0);
                    if (i < 12)
                    {
                        unloadStates.Add($"{i}: loaded=[{string.Join(';', biome.LoadedChunks.OrderBy(value => value.X).ThenBy(value => value.Y))}] " +
                            $"pending=[{string.Join(';', streaming.PendingUnloads.Keys.OrderBy(value => value.X).ThenBy(value => value.Y))}] " +
                            $"partial=[{string.Join(';', streaming.PartialUnloads.Select(pair => $"{pair.Key}:{pair.Value.Phase}/{pair.Value.RemovedEntityCells.Count}"))}]");
                    }

                    if (!biome.LoadedChunks.Contains(origin) &&
                        !streaming.PartialUnloads.ContainsKey(origin) &&
                        !biome.LoadedEntities.ContainsKey(origin))
                    {
                        break;
                    }
                }
                unloadDiagnostics = string.Join(" | ", unloadStates);
                pristineUnloaded = !em.EntityExists(pristine.Key);

                transform.SetWorldPosition(viewer, new Vector2(-4.5f, -4.5f));
                biomes.Update(0);
                var anchored = map.GetAnchoredEntities(mapUid, grid, pristine.Value);
                pristineRegenerated = anchored.MoveNext(out var regenerated) && regenerated != pristine.Key;
                anchored = map.GetAnchoredEntities(mapUid, grid, mined.Value);
                minedStayedEmpty = !anchored.MoveNext(out _);
                damagePreserved = em.EntityExists(damaged.Key) &&
                    em.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(damaged.Key).TotalDamage > 0;
                componentPreserved = em.HasComponent<PointLightComponent>(customized.Key);
                tagPreserved = em.System<TagSystem>().HasTag(tagged.Key, new ProtoId<TagPrototype>("Pickaxe"));
                gatherablePreserved = em.EntityExists(gatherableCustomized.Key) &&
                    em.GetComponent<Content.Server.Gatherable.Components.GatherableComponent>(gatherableCustomized.Key)
                        .GatherOffset > 1f;
            });

            Assert.Multiple(() =>
            {
                Assert.That(loadedInitially, Is.InRange(1, 3), "Background terrain must not generate the entire view range in one tick.");
                Assert.That(landingCellLoaded, Is.True,
                    "A downward transition must prepare one exact cell without completing the hidden chunk.");
                Assert.That(teleportedLandingCellLoaded, Is.True,
                    "The current physical viewer chunk must load before a stale Z-eye catches up.");
                Assert.That(pristineUnloaded, Is.True,
                    $"Pristine underground rocks must not accumulate permanently as modified terrain. {unloadDiagnostics}");
                Assert.That(pristineRegenerated, Is.True, unloadDiagnostics);
                Assert.That(minedStayedEmpty, Is.True, "Returning to a chunk must not respawn mined walls.");
                Assert.That(damagePreserved, Is.True);
                Assert.That(componentPreserved, Is.True);
                Assert.That(tagPreserved, Is.True);
                Assert.That(gatherablePreserved, Is.True,
                    "Changing a Gatherable gameplay field must keep the procedural entity persistent.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task NormalGhostViewSubscriptionsCannotGenerateAdjacentBiome()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var viewSubscribers = em.System<ViewSubscriberSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;
        var livingEyes = -1;
        var ghostEyes = -1;
        var loadedChunks = -1;

        try
        {
            await Server.WaitPost(() =>
            {
                var underground = map.CreateMap(runMapInit: false);
                var surface = map.CreateMap();
                biomes.EnsurePlanet(underground, Server.ProtoMan.Index(UndergroundBiome), 42);
                em.EnsureComponent<ClassicBiomeStreamingComponent>(underground);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [underground] = -1,
                });
                if (!map.IsInitialized(underground))
                    map.InitializeMap(underground);

                var ghost = em.SpawnEntity(null, new EntityCoordinates(surface, Vector2.Zero));
                Server.PlayerMan.SetAttachedEntity(session, ghost);
                var viewer = em.GetComponent<ClassicZLevelViewerComponent>(ghost);
                livingEyes = viewer.Eyes.Count;
                em.EnsureComponent<GhostComponent>(ghost);
                ghostEyes = viewer.Eyes.Count;

                // Exercise the defense-in-depth path too: even an arbitrary non-ghost camera
                // subscribed by a normal ghost must not be allowed to generate terrain.
                var auxiliaryViewer = em.SpawnEntity("ClassicZLevelEye",
                    new EntityCoordinates(underground, Vector2.Zero));
                viewSubscribers.AddViewSubscriber(auxiliaryViewer, session);

                biomes.Update(0);
                loadedChunks = em.GetComponent<BiomeComponent>(underground).LoadedChunks.Count;
            });

            Assert.Multiple(() =>
            {
                Assert.That(livingEyes, Is.GreaterThan(0),
                    "The regression setup must begin with a live lower-level Z-eye.");
                Assert.That(ghostEyes, Is.Zero, "Normal ghosts must not receive cross-Z PVS eyes.");
                Assert.That(loadedChunks, Is.Zero,
                    "A normal ghost's auxiliary view subscriptions must not generate biome chunks.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task IdleViewerPreloadsOnlyFallLandingUntilLookingUp()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var actions = em.System<SharedActionsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;
        var hadIdleUpperEye = true;
        var lowerCriticalChunks = -1;
        var lowerCriticalEntities = -1;
        var upperLandingChunks = -1;
        var upperLandingEntities = -1;
        var forcedCursorCells = -1;
        var keptLowerEye = false;
        var gainedUpperEye = false;

        try
        {
            await Server.WaitPost(() =>
            {
                var lower = map.CreateMap(runMapInit: false);
                var current = map.CreateMap();
                var upper = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(lower, Server.ProtoMan.Index(UndergroundBiome), 24);
                biomes.EnsurePlanet(upper, Server.ProtoMan.Index(UndergroundBiome), 42);
                var lowerStreaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(lower);
                lowerStreaming.BackgroundChunksPerTick = 2;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(upper);
                streaming.BackgroundChunksPerTick = 2;
                // WorkBudget is server-wide for active Classic biomes: one zero budget must stop
                // background work everywhere while still permitting exact safety chunks.
                streaming.WorkBudget = TimeSpan.Zero;

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [lower] = -1,
                    [current] = 0,
                    [upper] = 1,
                });
                if (!map.IsInitialized(lower))
                    map.InitializeMap(lower);
                if (!map.IsInitialized(upper))
                    map.InitializeMap(upper);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(current, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                var viewerComp = em.GetComponent<ClassicZLevelViewerComponent>(viewer);
                var lowerEye = viewerComp.Eyes.Single(eye => em.GetComponent<TransformComponent>(eye).MapUid == lower);
                hadIdleUpperEye = viewerComp.Eyes.Any(eye => em.GetComponent<TransformComponent>(eye).MapUid == upper);

                biomes.Update(0);
                var lowerBiome = em.GetComponent<BiomeComponent>(lower);
                lowerCriticalChunks = lowerBiome.LoadedChunks.Count;
                lowerCriticalEntities = lowerBiome.LoadedEntities.Values.Sum(chunk => chunk.Count);
                var upperBiome = em.GetComponent<BiomeComponent>(upper);
                upperLandingChunks = upperBiome.LoadedChunks.Count;
                upperLandingEntities = upperBiome.LoadedEntities.GetValueOrDefault(Vector2i.Zero)?.Count ?? 0;
                forcedCursorCells = lowerStreaming.PartialLoads.Values.Sum() + streaming.PartialLoads.Values.Sum();

                var action = actions.GetAction(viewerComp.ActionEntity);
                Assert.That(action, Is.Not.Null);
                actions.PerformAction(viewer, action!.Value);

                keptLowerEye = viewerComp.Eyes.Contains(lowerEye);
                gainedUpperEye = viewerComp.UpperEyes.Count == 1 &&
                    viewerComp.UpperEyes.All(eye => em.GetComponent<TransformComponent>(eye).MapUid == upper);
            });

            Assert.Multiple(() =>
            {
                Assert.That(hadIdleUpperEye, Is.False,
                    "The full upper PVS range must stay cold until the player looks up.");
                Assert.That(lowerCriticalChunks, Is.Zero,
                    "Foreground safety cells must not masquerade as a completed chunk.");
                Assert.That(lowerCriticalEntities, Is.InRange(1, 2),
                    "A render-only lower eye needs only the body's exact fall landing cell synchronously.");
                Assert.That(upperLandingChunks, Is.Zero,
                    "A grounded body must not activate the upper biome before it starts moving upward.");
                Assert.That(upperLandingEntities, Is.Zero);
                Assert.That(forcedCursorCells, Is.InRange(0, 1),
                    "A zero budget may advance at most one sequential background cell globally.");
                Assert.That(keptLowerEye, Is.True,
                    "Toggling LookUp must not recreate lower eyes or churn their PVS subscriptions.");
                Assert.That(gainedUpperEye, Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task RapidLookToggleReusesCachedUpperBiome()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var actions = em.System<SharedActionsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid network = default;
        EntityUid viewer = default;
        EntityUid upper = default;
        HashSet<EntityUid> cachedEntities = [];

        try
        {
            await Server.WaitPost(() =>
            {
                var current = map.CreateMap();
                upper = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(upper, Server.ProtoMan.Index(UndergroundBiome), 42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(upper);
                streaming.BackgroundChunksPerTick = 1;
                streaming.BackgroundCellsPerSlice = 1;
                streaming.UnloadEntitiesPerSlice = 64;
                streaming.WorkBudget = TimeSpan.FromSeconds(1);
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.ZLevelCacheDuration = TimeSpan.FromSeconds(10);

                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [current] = 0,
                    [upper] = 1,
                });
                if (!map.IsInitialized(upper))
                    map.InitializeMap(upper);

                viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(current, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                var viewerComp = em.GetComponent<ClassicZLevelViewerComponent>(viewer);
                var action = actions.GetAction(viewerComp.ActionEntity);
                Assert.That(action, Is.Not.Null);
                actions.PerformAction(viewer, action!.Value);

                Assert.That(viewerComp.UpperEyes, Has.Count.EqualTo(1));
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(upper);
                Assert.That(streaming.PartialLoads, Is.Not.Empty);
                cachedEntities = biome.LoadedEntities.Values.SelectMany(chunk => chunk.Keys).ToHashSet();
                Assert.That(cachedEntities, Is.Not.Empty);

                actions.PerformAction(viewer, action.Value);
                Assert.That(viewerComp.UpperEyes, Is.Empty);
            });

            await Server.WaitRunTicks(3);

            await Server.WaitPost(() =>
            {
                var biome = em.GetComponent<BiomeComponent>(upper);
                var streaming = em.GetComponent<ClassicBiomeStreamingComponent>(upper);
                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedEntities.Values.SelectMany(chunk => chunk.Keys),
                        Is.SupersetOf(cachedEntities));
                    Assert.That(streaming.PartialUnloads, Is.Empty);
                });

                var viewerComp = em.GetComponent<ClassicZLevelViewerComponent>(viewer);
                var action = actions.GetAction(viewerComp.ActionEntity);
                Assert.That(action, Is.Not.Null);
                actions.PerformAction(viewer, action!.Value);
                Assert.That(viewerComp.UpperEyes, Has.Count.EqualTo(1));
                biomes.Update(0);

                Assert.That(
                    biome.LoadedEntities.Values.SelectMany(chunk => chunk.Keys),
                    Is.SupersetOf(cachedEntities),
                    "Rapidly restoring the upper eye must reuse resident biome entities.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    public async Task SustainedDistantMovementKeepsAndDrainsBoundedBiomeWorkingSet()
    {
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetMaxUpdateRange, 16f);
        await OverrideCVar(Side.Server, Robust.Shared.CVars.NetPvsPriorityRange, 16f);
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var transform = em.System<SharedTransformSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        EntityUid terrain = default;
        var backlogLimit = 2;
        var backgroundChunksPerTick = 4;
        var backgroundEntitySpawnsPerTick = 4;
        var viewerSafetyRadius = 1;
        var maxZLevelCachedChunks = 2;
        var residentLimit = backlogLimit + backgroundChunksPerTick + maxZLevelCachedChunks;
        var entitiesPerResident = (viewerSafetyRadius * 2 + 1) * (viewerSafetyRadius * 2 + 1) +
                                  backgroundEntitySpawnsPerTick * 2;
        var maxResident = 0;
        var maxLoadedEntities = 0;
        var maxPendingUnloads = 0;
        var maxPartialLoads = 0;
        var maxPartialUnloads = 0;
        var maxCachedZChunks = 0;
        var backgroundSpawnObserved = false;

        try
        {
            await Server.WaitPost(() =>
            {
                terrain = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(terrain, Server.ProtoMan.Index(UndergroundBiome), 42);
                em.GetComponent<MapGridComponent>(terrain).CanSplit = false;
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(terrain);
                streaming.BackgroundChunksPerTick = backgroundChunksPerTick;
                streaming.BackgroundCellsPerSlice = 64;
                streaming.BackgroundEntitySpawnsPerTick = backgroundEntitySpawnsPerTick;
                streaming.ViewerSafetyRadius = viewerSafetyRadius;
                streaming.UnloadEntitiesPerSlice = 4;
                streaming.UnloadEntitiesPerTick = 16;
                streaming.UnloadCellsPerSlice = 16;
                streaming.UnloadDelay = TimeSpan.FromSeconds(1);
                streaming.MaxUnloadBacklogBeforeThrottling = backlogLimit;
                streaming.ZLevelCacheDuration = TimeSpan.FromSeconds(1);
                streaming.MaxZLevelCachedChunks = maxZLevelCachedChunks;
                streaming.WorkBudget = TimeSpan.Zero;
                map.InitializeMap(terrain);

                var viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(terrain, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                var biome = em.GetComponent<BiomeComponent>(terrain);

                void SampleWorkingSet()
                {
                    var resident = biome.LoadedChunks
                        .Concat(biome.LoadedEntities.Keys)
                        .Concat(streaming.PartialLoads.Keys)
                        .Concat(streaming.PartialUnloads.Keys)
                        .Concat(streaming.PendingUnloads.Keys)
                        .Concat(streaming.CachedZChunks.Keys)
                        .ToHashSet();
                    maxResident = Math.Max(maxResident, resident.Count);
                    maxLoadedEntities = Math.Max(
                        maxLoadedEntities,
                        biome.LoadedEntities.Values.Sum(chunk => chunk.Count));
                    maxPendingUnloads = Math.Max(maxPendingUnloads, streaming.PendingUnloads.Count);
                    maxPartialLoads = Math.Max(maxPartialLoads, streaming.PartialLoads.Count);
                    maxPartialUnloads = Math.Max(maxPartialUnloads, streaming.PartialUnloads.Count);
                    maxCachedZChunks = Math.Max(maxCachedZChunks, streaming.CachedZChunks.Count);
                }

                for (var i = 0; i < 48; i++)
                {
                    var position = new Vector2(i * 48f + 4.5f, 4.5f);
                    biomes.CacheClassicZLevel(viewer);
                    transform.SetWorldPosition(viewer, position);
                    biomes.Update(0);
                    SampleWorkingSet();
                    var entitiesBeforeBackground = biome.LoadedEntities.Values
                        .SelectMany(chunk => chunk.Keys)
                        .ToHashSet();
                    biomes.Update(0);
                    SampleWorkingSet();
                    var entitiesAfterBackground = biome.LoadedEntities.Values
                        .SelectMany(chunk => chunk.Keys)
                        .ToHashSet();
                    var spawned = entitiesAfterBackground.Except(entitiesBeforeBackground).Count();
                    backgroundSpawnObserved |= spawned > 0;
                    Assert.That(
                        spawned,
                        Is.InRange(0, backgroundEntitySpawnsPerTick));
                }

                Assert.Multiple(() =>
                {
                    Assert.That(backgroundSpawnObserved, Is.True);
                    Assert.That(maxLoadedEntities, Is.GreaterThan(0));
                    Assert.That(maxResident, Is.LessThanOrEqualTo(residentLimit));
                    Assert.That(maxLoadedEntities, Is.LessThanOrEqualTo(residentLimit * entitiesPerResident));
                    Assert.That(maxPendingUnloads, Is.LessThanOrEqualTo(residentLimit));
                    Assert.That(maxPartialLoads, Is.LessThanOrEqualTo(residentLimit));
                    Assert.That(maxPartialUnloads, Is.LessThanOrEqualTo(residentLimit));
                    Assert.That(maxCachedZChunks, Is.InRange(1, maxZLevelCachedChunks));
                });

                Server.PlayerMan.SetAttachedEntity(session, originalViewer);
            });

            var drainTicks = (int) Math.Ceiling(3d / Server.Timing.TickPeriod.TotalSeconds);
            await Server.WaitRunTicks(drainTicks);

            await Server.WaitPost(() =>
            {
                var biome = em.GetComponent<BiomeComponent>(terrain);
                var streaming = em.GetComponent<ClassicBiomeStreamingComponent>(terrain);
                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedChunks, Is.Empty);
                    Assert.That(biome.LoadedEntities, Is.Empty);
                    Assert.That(biome.LoadedDecals, Is.Empty);
                    Assert.That(streaming.PartialLoads, Is.Empty);
                    Assert.That(streaming.PartialUnloads, Is.Empty);
                    Assert.That(streaming.PendingUnloads, Is.Empty);
                    Assert.That(streaming.PriorityLoadedCells, Is.Empty);
                    Assert.That(streaming.MaterializedCells, Is.Empty);
                    Assert.That(streaming.CachedZChunks, Is.Empty);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (terrain != EntityUid.Invalid)
                await Server.WaitPost(() => em.DeleteEntity(terrain));
        }
    }

    [Test]
    public async Task MovingBetweenZLevelsCachesPreviousBiome()
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var biomes = em.System<BiomeSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var session = ServerSession!;
        var originalViewer = session.AttachedEntity;
        Entity<ClassicZMapNetworkComponent> network = default;
        EntityUid viewer = default;
        EntityUid source = default;
        HashSet<EntityUid> cachedEntities = [];

        try
        {
            await Server.WaitPost(() =>
            {
                var target = map.CreateMap();
                source = map.CreateMap(runMapInit: false);
                biomes.EnsurePlanet(source, Server.ProtoMan.Index(UndergroundBiome), 42);
                var streaming = em.EnsureComponent<ClassicBiomeStreamingComponent>(source);
                streaming.BackgroundChunksPerTick = 0;
                streaming.BackgroundCellsPerSlice = 1;
                streaming.UnloadEntitiesPerSlice = 64;
                streaming.WorkBudget = TimeSpan.Zero;
                streaming.UnloadDelay = TimeSpan.Zero;
                streaming.ZLevelCacheDuration = TimeSpan.FromSeconds(10);

                network = zLevels.CreateMapNetwork();
                zLevels.TryAddMapsIntoNetwork(network, new Dictionary<EntityUid, int>
                {
                    [target] = 0,
                    [source] = 1,
                });
                if (!map.IsInitialized(source))
                    map.InitializeMap(source);

                viewer = em.SpawnEntity(
                    "ClassicStreamingPhysicalViewerTest",
                    new EntityCoordinates(source, new Vector2(4.5f, 4.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                biomes.Update(0);

                var biome = em.GetComponent<BiomeComponent>(source);
                Assert.That(streaming.PartialLoads, Is.Not.Empty);
                cachedEntities = biome.LoadedEntities.Values.SelectMany(chunk => chunk.Keys).ToHashSet();
                Assert.That(cachedEntities, Is.Not.Empty);

                Assert.That(zLevels.TryMoveDown(viewer), Is.True);
                Assert.That(streaming.CachedZChunks, Is.Not.Empty);
            });

            await Server.WaitRunTicks(3);

            await Server.WaitPost(() =>
            {
                var biome = em.GetComponent<BiomeComponent>(source);
                var streaming = em.GetComponent<ClassicBiomeStreamingComponent>(source);
                Assert.Multiple(() =>
                {
                    Assert.That(biome.LoadedEntities.Values.SelectMany(chunk => chunk.Keys),
                        Is.SupersetOf(cachedEntities));
                    Assert.That(streaming.PartialUnloads, Is.Empty);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalViewer));
            if (network.Owner != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }
}
