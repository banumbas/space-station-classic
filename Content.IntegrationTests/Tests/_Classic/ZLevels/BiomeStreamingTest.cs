using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Classic.Station;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.Parallax;
using Content.Shared.Damage;
using Content.Shared._Classic.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Tag;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Classic.ZLevels;

// This regression fixture controls streaming budgets and inspects generated terrain directly.
#pragma warning disable RA0002

[TestFixture]
[EnsureCVar(Side.Server, typeof(ClassicCCVars), nameof(ClassicCCVars.AtmosEnabled), true)]
public sealed class BiomeStreamingTest : GameTest
{
    private static readonly ProtoId<BiomeTemplatePrototype> UndergroundBiome = "ClassicUndergroundDirtStone";

    public override PoolSettings PoolSettings => new() { Connected = true };

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
        var loadedInitially = 0;
        var landingCellLoaded = false;
        var teleportedLandingCellLoaded = false;

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
                streaming.UnloadDelay = TimeSpan.Zero;
                var zNetwork = zLevels.CreateMapNetwork();
                network = zNetwork;
                zLevels.TryAddMapsIntoNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [surface] = 0,
                    [mapUid] = -1,
                });
                if (!map.IsInitialized(mapUid))
                    map.InitializeMap(mapUid);
                var viewer = em.SpawnEntity(null, new EntityCoordinates(surface, new Vector2(-0.5f, -0.5f)));
                Server.PlayerMan.SetAttachedEntity(session, viewer);

                biomes.Update(0);
                var biome = em.GetComponent<BiomeComponent>(mapUid);
                loadedInitially = biome.LoadedChunks.Count;
                var origin = new Vector2i(-8, -8);
                landingCellLoaded = biome.LoadedEntities.TryGetValue(origin, out var loaded) && loaded.Count == 64;
                var rocks = loaded!.Take(5).ToArray();
                var pristine = rocks[0];
                var mined = rocks[1];
                var damaged = rocks[2];
                var customized = rocks[3];
                var tagged = rocks[4];

                em.DeleteEntity(mined.Key);
                em.System<DamageableSystem>().TryChangeDamage(damaged.Key,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, ignoreResistances: true);
                em.EnsureComponent<PointLightComponent>(customized.Key);
                em.System<TagSystem>().AddTag(tagged.Key, new ProtoId<TagPrototype>("Pickaxe"));

                // Leave the chunk and let its unload deadline pass. No background loading is
                // needed for this part, so the return trip exercises exactly the original chunk.
                // Z-eyes still point at the old location until their periodic update; adjacent
                // terrain must already use the current owner position for landing safety.
                streaming.BackgroundChunksPerTick = 0;
                transform.SetWorldPosition(viewer, new Vector2(1000.5f, 1000.5f));
                biomes.Update(0);
                teleportedLandingCellLoaded = biome.LoadedEntities.TryGetValue(new Vector2i(1000, 1000), out var destination) &&
                    destination.Count == 64;
                for (var i = 0; i < 4; i++)
                    biomes.Update(0);
                pristineUnloaded = !em.EntityExists(pristine.Key);

                transform.SetWorldPosition(viewer, new Vector2(-0.5f, -0.5f));
                biomes.Update(0);
                var anchored = map.GetAnchoredEntities(mapUid, grid, pristine.Value);
                pristineRegenerated = anchored.MoveNext(out var regenerated) && regenerated != pristine.Key;
                anchored = map.GetAnchoredEntities(mapUid, grid, mined.Value);
                minedStayedEmpty = !anchored.MoveNext(out _);
                damagePreserved = em.EntityExists(damaged.Key) &&
                    em.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(damaged.Key).TotalDamage > 0;
                componentPreserved = em.HasComponent<PointLightComponent>(customized.Key);
                tagPreserved = em.System<TagSystem>().HasTag(tagged.Key, new ProtoId<TagPrototype>("Pickaxe"));
            });

            Assert.Multiple(() =>
            {
                Assert.That(loadedInitially, Is.InRange(1, 3), "Background terrain must not generate the entire view range in one tick.");
                Assert.That(landingCellLoaded, Is.True, "The viewer's chunk must include its solid walls immediately.");
                Assert.That(teleportedLandingCellLoaded, Is.True, "The adjacent landing chunk must load before the Z-eye catches up.");
                Assert.That(pristineUnloaded, Is.True, "Pristine underground rocks must not accumulate permanently as modified terrain.");
                Assert.That(pristineRegenerated, Is.True);
                Assert.That(minedStayedEmpty, Is.True, "Returning to a chunk must not respawn mined walls.");
                Assert.That(damagePreserved, Is.True);
                Assert.That(componentPreserved, Is.True);
                Assert.That(tagPreserved, Is.True);
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
}
