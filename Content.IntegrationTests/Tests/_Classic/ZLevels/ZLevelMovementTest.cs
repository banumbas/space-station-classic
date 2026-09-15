using System.Collections.Generic;
using Content.Client._Classic.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Classic.ZLevels;

[TestFixture]
public sealed class ZLevelMovementTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    private static PoolSettings ConnectedClient => new() { Connected = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: TestClassicZBody
          components:
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              body:
                shape: !type:PhysShapeCircle
                  radius: 0.2
                density: 1
                layer: [SmallMobLayer]
                mask: [SmallMobMask]
          - type: ClassicZPhysics
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task SparseTileEditsPreserveSleepingBodiesAndRemovedFloorStillFalls(bool wallBelow)
    {
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var tiles = Server.ResolveDependency<ITileDefinitionManager>();
        EntityUid network = default;
        EntityUid lowerMap = default;
        Entity<MapGridComponent> upper = default;
        EntityUid body = default;
        EntityUid wall = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var upperMap = map.CreateMap();
                lowerMap = map.CreateMap();
                upper = map.CreateGridEntity(upperMap);
                var lower = map.CreateGridEntity(lowerMap);
                upper.Comp.CanSplit = false;
                lower.Comp.CanSplit = false;
                var plating = new Tile(tiles["Plating"].TileId);
                map.SetTile(upper, Vector2i.Zero, plating);
                map.SetTile(lower, Vector2i.Zero, plating);
                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [upperMap] = 0,
                    [lowerMap] = -1,
                });
                if (wallBelow)
                    wall = em.SpawnEntity("WallSolid", map.GridTileToLocal(lower, lower.Comp, Vector2i.Zero));
                body = em.SpawnEntity("TestClassicZBody", map.GridTileToLocal(upper, upper.Comp, Vector2i.Zero));
            });

            await Server.WaitRunTicks(3);
            await Server.WaitPost(() =>
            {
                zLevels.SleepBody(body);
                map.SetTile(upper, Vector2i.Zero, new Tile(tiles["ClassicStone"].TileId));
                var plating = new Tile(tiles["Plating"].TileId);
                map.SetTiles(upper, upper.Comp, new List<(Vector2i, Tile)>
                {
                    (new Vector2i(-64, 0), plating),
                    (new Vector2i(64, 0), plating),
                });
            });
            await Server.WaitRunTicks(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(em.GetComponent<ClassicZPhysicsComponent>(body).Sleeping, Is.True,
                    "Distant edits in one batch must not wake bodies between those edits.");
                Assert.That(zLevels.ActiveBodies, Does.Not.Contain(body));
            });

            await Server.WaitPost(() => map.SetTile(upper, Vector2i.Zero, Tile.Empty));
            await Server.WaitRunTicks(6);
            if (wallBelow)
            {
                await Server.WaitAssertion(() =>
                {
                    Assert.That(em.GetComponent<TransformComponent>(body).MapUid,
                        Is.EqualTo(em.GetComponent<TransformComponent>(upper).MapUid),
                        "A wall below must support the body after the upper floor is removed.");
                });
                await Server.WaitPost(() =>
                {
                    zLevels.SleepBody(body);
                    em.DeleteEntity(wall);
                });
                await Server.WaitRunTicks(6);
            }
            await Server.WaitAssertion(() =>
            {
                Assert.That(em.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(lowerMap),
                    "Removing the floor must wake a sleeping body and let it fall to the lower map.");
                Assert.That(zLevels.ActiveBodies, Has.Exactly(1).EqualTo(body));
            });
        }
        finally
        {
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }

    [Test]
    [PairConfig(nameof(ConnectedClient))]
    public async Task ClientOpeningBoundsIncludeAllHolesAndShrinkAfterClosingOne()
    {
        var em = Client.EntMan;
        var map = em.System<SharedMapSystem>();
        var cache = em.System<ClassicClientZLevelsSystem>().OpeningCache;
        var tiles = Client.ResolveDependency<ITileDefinitionManager>();
        EntityUid mapUid = default;
        try
        {
            await Client.WaitAssertion(() =>
            {
                mapUid = map.CreateMap();
                var grid = map.CreateGridEntity(mapUid);
                grid.Comp.CanSplit = false;
                var plating = new Tile(tiles["Plating"].TileId);
                var batch = new List<(Vector2i, Tile)>();
                for (var x = -16; x < 16; x++)
                for (var y = -16; y < 16; y++)
                    batch.Add((new Vector2i(x, y), plating));
                map.SetTiles(grid, grid.Comp, batch);
                var start = new Vector2i(-16, -16);
                var end = new Vector2i(15, 15);
                Assert.That(cache.TryGetOpeningBounds(grid, start, end, map, tiles, out _), Is.False);

                var first = new Vector2i(-10, 4);
                var second = new Vector2i(12, -8);
                map.SetTile(grid, first, Tile.Empty);
                map.SetTile(grid, second, Tile.Empty);
                Assert.That(cache.TryGetOpeningBounds(grid, start, end, map, tiles, out var bounds), Is.True);
                Assert.That(bounds, Is.EqualTo(new Box2(-10, -8, 13, 5)));

                map.SetTile(grid, first, plating);
                Assert.That(cache.TryGetOpeningBounds(grid, end, start, map, tiles, out bounds), Is.True);
                Assert.That(bounds, Is.EqualTo(new Box2(12, -8, 13, -7)));
                map.SetTile(grid, second, plating);
                Assert.That(cache.TryGetOpeningBounds(grid, start, end, map, tiles, out _), Is.False);
            });
        }
        finally
        {
            if (mapUid != EntityUid.Invalid)
                await Client.WaitPost(() => em.DeleteEntity(mapUid));
        }
    }

    [Test]
    [PairConfig(nameof(ConnectedClient))]
    public async Task ClientOpeningCacheTracksMultipleTileChangesInOneTick()
    {
        var em = Client.EntMan;
        var map = em.System<SharedMapSystem>();
        var cache = em.System<ClassicClientZLevelsSystem>().OpeningCache;
        var tiles = Client.ResolveDependency<ITileDefinitionManager>();
        EntityUid mapUid = default;
        var before = true;
        var removed = false;
        var replaced = true;

        try
        {
            await Client.WaitPost(() =>
            {
                mapUid = map.CreateMap();
                var grid = map.CreateGridEntity(mapUid);
                grid.Comp.CanSplit = false;
                var index = new Vector2i(-1, -1);
                var plating = new Tile(tiles["Plating"].TileId);
                map.SetTile(grid, index, plating);
                before = cache.HasOpeningInTileBounds(grid, index, index, map, tiles);
                map.SetTile(grid, index, Tile.Empty);
                removed = cache.HasOpeningInTileBounds(grid, index, index, map, tiles);
                map.SetTile(grid, index, plating);
                replaced = cache.HasOpeningInTileBounds(grid, index, index, map, tiles);
            });
            Assert.Multiple(() =>
            {
                Assert.That(before, Is.False);
                Assert.That(removed, Is.True, "Opening a floor must invalidate the cached render decision immediately.");
                Assert.That(replaced, Is.False, "Closing the opening in the same tick must invalidate it again.");
            });
        }
        finally
        {
            if (mapUid != EntityUid.Invalid)
                await Client.WaitPost(() => em.DeleteEntity(mapUid));
        }
    }
}
