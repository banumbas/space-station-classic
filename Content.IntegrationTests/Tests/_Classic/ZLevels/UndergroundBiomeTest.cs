using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Classic.Station;
using Content.Server._Classic.ZCollapse;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Station.Components;
using Content.Shared._Classic.Station.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Classic.ZLevels;

[TestFixture]
public sealed class UndergroundBiomeTest : GameTest
{
    private static readonly string MapPrototypeId = "ClassicClassic";

    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task ClassicUndergroundSetupContractsTest()
    {
        var server = Pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
        var ticker = entManager.System<GameTicker>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var zLevels = entManager.System<ClassicZLevelsSystem>();
        var stationSystem = entManager.System<StationSystem>();

        var networkUid = EntityUid.Invalid;

        try
        {
            await server.WaitAssertion(() =>
            {
                var options = DeserializationOptions.Default with { InitializeMaps = true };
                ticker.LoadGameMap(protoManager.Index<GameMapPrototype>(MapPrototypeId), out var surfaceMapId, options);

                var surfaceGrid = mapSystem.GetAllGrids(surfaceMapId)
                    .MaxBy(grid => grid.Comp.ChunkCount);
                Assert.That(surfaceGrid.Owner, Is.Not.EqualTo(EntityUid.Invalid), "Classic surface has no terrain grid.");

                var surfaceMapUid = entManager.GetComponent<TransformComponent>(surfaceGrid.Owner).MapUid;
                Assert.That(surfaceMapUid, Is.Not.Null, "Classic surface grid has no parent map.");

                var surfaceZMap = entManager.GetComponent<ClassicZMapComponent>(surfaceMapUid!.Value);
                networkUid = surfaceZMap.NetworkUid;
                var network = entManager.GetComponent<ClassicZMapNetworkComponent>(networkUid);

                Assert.Multiple(() =>
                {
                    Assert.That(surfaceZMap.Depth, Is.Zero);
                    Assert.That(network.ZLevels.Keys, Is.EquivalentTo(new[] { -3, -2, -1, 0, 1 }));
                    Assert.That(network.SortedMin, Is.EqualTo(-3));
                    Assert.That(network.SortedMax, Is.EqualTo(1));
                    Assert.That(network.SortedZLevels, Has.Count.EqualTo(5));
                });

                var surfaceBiome = entManager.GetComponent<BiomeComponent>(surfaceGrid.Owner);
                var stationUid = entManager.GetComponent<StationMemberComponent>(surfaceGrid.Owner).Station;
                var stationData = entManager.GetComponent<StationDataComponent>(stationUid);
                Assert.Multiple(() =>
                {
                    Assert.That(surfaceBiome.Template?.Id, Is.EqualTo("GrasslandsClassic"));
                    Assert.That(entManager.HasComponent<ClassicGridStabilityComponent>(surfaceGrid.Owner), Is.True);
                });

                var expectedLevels = new Dictionary<int, (string Template, bool LoadEntities, bool LoadDecals, string Parallax)>
                {
                    [-1] = ("ClassicUndergroundDirtStone", true, false, "Dirt"),
                    [-2] = ("ClassicUndergroundCaves", true, false, "Dirt"),
                    [-3] = ("ClassicUndergroundMagma", true, false, "ClassicMagma"),
                };

                foreach (var (depth, expected) in expectedLevels)
                {
                    Assert.That(network.ZLevels.TryGetValue(depth, out var undergroundMapUid), Is.True);
                    Assert.That(undergroundMapUid, Is.Not.Null, $"No map exists at depth {depth}.");

                    var mapUid = undergroundMapUid!.Value;
                    var zMap = entManager.GetComponent<ClassicZMapComponent>(mapUid);
                    var undergroundGrid = GetTerrainGrid(mapUid, entManager, mapSystem);
                    var marker = entManager.GetComponent<ClassicUndergroundBiomeComponent>(undergroundGrid.Owner);
                    var biome = entManager.GetComponent<BiomeComponent>(undergroundGrid.Owner);

                    Assert.Multiple(() =>
                    {
                        Assert.That(zMap.Depth, Is.EqualTo(depth));
                        Assert.That(zMap.MapAbove, Is.EqualTo(network.ZLevels[depth + 1]));
                        Assert.That(zMap.MapBelow, Is.EqualTo(depth == -3 ? null : network.ZLevels[depth - 1]));
                        Assert.That(marker.Depth, Is.EqualTo(depth));
                        Assert.That(marker.LoadEntities, Is.EqualTo(expected.LoadEntities));
                        Assert.That(marker.LoadDecals, Is.EqualTo(expected.LoadDecals));
                        Assert.That(biome.Template?.Id, Is.EqualTo(expected.Template));
                        Assert.That(biome.Seed, Is.EqualTo(surfaceBiome.Seed), "All station biomes must share one seed.");
                        Assert.That(entManager.GetComponent<ParallaxComponent>(mapUid).Parallax, Is.EqualTo(expected.Parallax));
                        Assert.That(entManager.HasComponent<ClassicGridStabilityComponent>(undergroundGrid.Owner), Is.False);
                        Assert.That(entManager.HasComponent<StationAuxiliaryGridComponent>(undergroundGrid.Owner), Is.True);
                        Assert.That(entManager.GetComponent<StationMemberComponent>(undergroundGrid.Owner).Station,
                            Is.EqualTo(stationUid));
                        Assert.That(stationData.Grids, Does.Not.Contain(undergroundGrid.Owner));
                        Assert.That(stationData.AuxiliaryGrids, Does.Contain(undergroundGrid.Owner));
                        Assert.That(entManager.HasComponent<SunShadowComponent>(undergroundGrid.Owner), Is.False);
                        Assert.That(entManager.HasComponent<SunShadowCycleComponent>(undergroundGrid.Owner), Is.False);
                        Assert.That(entManager.HasComponent<LightCycleComponent>(mapUid), Is.False);
                    });

                    AssertAlignedTileCenter(Vector2i.Zero, surfaceGrid, undergroundGrid, mapSystem, depth);
                    AssertAlignedTileCenter(new Vector2i(7, 7), surfaceGrid, undergroundGrid, mapSystem, depth);

                    if (depth == -1)
                        AssertStreamedLowerTileGetsRoof(surfaceGrid, undergroundGrid, mapSystem, entManager, tileDefinitions);
                }

                Assert.That(stationSystem.GetLargestGrid(stationUid), Is.EqualTo(surfaceGrid.Owner),
                    "Explored underground terrain must never replace the station's primary grid.");
            });
        }
        finally
        {
            if (networkUid != EntityUid.Invalid)
            {
                await server.WaitPost(() => zLevels.DeleteMapNetwork(networkUid));
                await server.WaitRunTicks(2);
            }
        }
    }

    private static void AssertStreamedLowerTileGetsRoof(
        Entity<MapGridComponent> surface,
        Entity<MapGridComponent> underground,
        SharedMapSystem mapSystem,
        IEntityManager entManager,
        ITileDefinitionManager tileDefinitions)
    {
        Vector2i? coveredIndex = null;
        var tiles = mapSystem.GetAllTilesEnumerator(surface.Owner, surface.Comp);
        while (tiles.MoveNext(out var tileRef))
        {
            if (tileRef.Value.Tile.IsEmpty ||
                tileDefinitions[tileRef.Value.Tile.TypeId] is not ContentTileDefinition { Transparent: false })
            {
                continue;
            }

            coveredIndex = tileRef.Value.GridIndices;
            break;
        }

        Assert.That(coveredIndex, Is.Not.Null, "Classic surface has no opaque tile for the roof-streaming contract.");

        var stone = tileDefinitions["ClassicStone"];
        mapSystem.SetTile(underground.Owner, underground.Comp, coveredIndex!.Value, new Tile(stone.TileId));

        var roof = entManager.GetComponent<RoofComponent>(underground.Owner);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(coveredIndex.Value, RoofComponent.ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(coveredIndex.Value, RoofComponent.ChunkSize);
        var bit = (ulong) 1 << (relative.X + relative.Y * RoofComponent.ChunkSize);

        Assert.That(roof.Data.TryGetValue(chunkOrigin, out var mask) && (mask & bit) != 0,
            Is.True,
            "A lower tile streamed after an opaque surface tile must be roofed immediately.");
    }

    private static Entity<MapGridComponent> GetTerrainGrid(
        EntityUid mapUid,
        IEntityManager entManager,
        SharedMapSystem mapSystem)
    {
        if (entManager.TryGetComponent<MapGridComponent>(mapUid, out var combinedGrid))
            return (mapUid, combinedGrid);

        var map = entManager.GetComponent<MapComponent>(mapUid);
        return mapSystem.GetAllGrids(map.MapId).MaxBy(grid => grid.Comp.ChunkCount);
    }

    private static void AssertAlignedTileCenter(
        Vector2i indices,
        Entity<MapGridComponent> surface,
        Entity<MapGridComponent> underground,
        SharedMapSystem mapSystem,
        int depth)
    {
        var surfacePosition = mapSystem.GridTileToWorldPos(surface.Owner, surface.Comp, indices);
        var undergroundPosition = mapSystem.GridTileToWorldPos(underground.Owner, underground.Comp, indices);

        Assert.Multiple(() =>
        {
            Assert.That(undergroundPosition.X, Is.EqualTo(surfacePosition.X).Within(0.0001f),
                $"X tile-center alignment differs at depth {depth} for {indices}.");
            Assert.That(undergroundPosition.Y, Is.EqualTo(surfacePosition.Y).Within(0.0001f),
                $"Y tile-center alignment differs at depth {depth} for {indices}.");
        });
    }
}
