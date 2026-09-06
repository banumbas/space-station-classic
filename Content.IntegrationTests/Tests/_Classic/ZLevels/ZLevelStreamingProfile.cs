using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared._Classic.CCVar;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Tag;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._Classic.ZLevels;

/// <summary>
/// Reproducible server profiling scenario; run explicitly in Release. It measures the real system
/// updates and allocations, without rendering or a wall-clock timing assertion on CI hardware.
/// </summary>
[TestFixture, Explicit]
[EnsureCVar(Side.Server, typeof(CVars), nameof(CVars.ProfEnabled), true)]
public sealed class ZLevelStreamingProfile : GameTest
{
    private static readonly ProtoId<GameMapPrototype> Colony = "ClassicClassic";

    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    [EnsureCVar(Side.Server, typeof(Content.Shared.CCVar.CCVars), nameof(Content.Shared.CCVar.CCVars.GridFill), false)]
    public async Task ProfileColonyStreaming()
    {
        // Keep comparisons reproducible even when the server's default changes.
        await OverrideCVar(Side.Server, ClassicCCVars.AtmosEnabled, true);
        var em = Server.EntMan;
        var map = em.System<SharedMapSystem>();
        var zLevels = em.System<ClassicZLevelsSystem>();
        var transform = em.System<SharedTransformSystem>();
        var profiler = Server.ResolveDependency<ProfManager>();
        var sessions = await Server.AddDummySessions(1);
        EntityUid network = default;
        EntityUid viewer = default;
        EntityUid surface = default;
        var samples = new Dictionary<string, List<TimeAndAllocSample>>();
        long cursor = 0;

        try
        {
            await Server.WaitPost(() =>
            {
                Server.ResolveDependency<IRobustRandom>().SetSeed(42);
                var prototype = Server.ProtoMan.Index(Colony);
                em.System<GameTicker>().LoadGameMap(prototype, out var mapId,
                    DeserializationOptions.Default with { InitializeMaps = true });
                surface = map.GetMap(mapId);
                network = em.GetComponent<ClassicZMapComponent>(surface).NetworkUid;
                viewer = em.SpawnEntity(null, new EntityCoordinates(surface, new Vector2(1000.1f, 1000.1f)));
                Server.PlayerMan.SetAttachedEntity(sessions[0], viewer);
            });
            await Server.WaitRunTicks(90);
            await Server.WaitPost(() =>
            {
                var grid = map.GetAllGrids(em.GetComponent<TransformComponent>(surface).MapID)
                    .MaxBy(candidate => candidate.Comp.ChunkCount);
                var roofSystem = em.System<SharedRoofSystem>();
                var roof = em.GetComponent<RoofComponent>(grid);
                var center = map.WorldToTile(grid, grid.Comp, new Vector2(1000, 1000));
                var bounds = new Box2(center.X - 17, center.Y - 17, center.X + 17, center.Y + 17);
                var counts = new int[2];
                for (var mode = 0; mode < 2; mode++)
                {
                    var warmEntities = mode == 0 || roofSystem.HasRoofEntities(grid, bounds);
                    for (var x = center.X - 16; x < center.X + 16; x++)
                    for (var y = center.Y - 16; y < center.Y + 16; y++)
                        roofSystem.GetColor((grid.Owner, grid.Comp, roof), new Vector2i(x, y), warmEntities);

                    var start = Stopwatch.GetTimestamp();
                    for (var frame = 0; frame < 20; frame++)
                    {
                        var checkEntities = mode == 0 || roofSystem.HasRoofEntities(grid, bounds);
                        for (var x = center.X - 16; x < center.X + 16; x++)
                        for (var y = center.Y - 16; y < center.Y + 16; y++)
                        {
                            if (roofSystem.GetColor((grid.Owner, grid.Comp, roof), new Vector2i(x, y), checkEntities) != null)
                                counts[mode]++;
                        }
                    }
                    TestContext.Out.WriteLine($"ROOF_SCAN mode={mode} tiles=20480 elapsed_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F3}");
                }
                Assert.That(counts[1], Is.EqualTo(counts[0]), "Batched roof checks must retain the same rendered tile colors.");
            });
            await Server.WaitPost(() =>
            {
                var lower = em.GetComponent<ClassicZMapNetworkComponent>(network).ZLevels[-1]!.Value;
                var biome = em.GetComponent<BiomeComponent>(lower);
                var rock = biome.LoadedEntities.Values.First(chunk => chunk.Count > 0).Keys.First();
                var metadata = em.GetComponent<MetaDataComponent>(rock);
                var prototype = metadata.EntityPrototype!;
                var serializer = Server.ResolveDependency<ISerializationManager>();
                TestContext.Out.WriteLine($"ROCK default={em.IsDefault(rock)} prototype={prototype.ID} components={em.GetComponents(rock).Count()} expected={prototype.Components.Count + 2}");
                TestContext.Out.WriteLine($"ROCK tags={string.Join(',', em.GetComponent<TagComponent>(rock).Tags)} prototype_components={string.Join(',', prototype.Components.Keys)}");
                TestContext.Out.WriteLine($"ROCK components={string.Join(',', em.GetComponents(rock).Select(comp => em.ComponentFactory.GetRegistration(comp.GetType()).Name))}");
                foreach (var component in em.GetComponents(rock))
                {
                    var type = component.GetType();
                    if (component is TransformComponent or MetaDataComponent)
                        continue;
                    var name = em.ComponentFactory.GetRegistration(type).Name;
                    if (!prototype.Components.TryGetValue(name, out var baseline))
                        TestContext.Out.WriteLine($"ROCK added={name}");
                    else if (!serializer.DataFieldEquals(type, component, baseline.Component))
                        TestContext.Out.WriteLine($"ROCK changed={name}");
                }
            });

            foreach (var phase in new[] { "idle", "boundary", "exploration", "recovery" })
            {
                await Server.WaitPost(() =>
                {
                    samples.Clear();
                    cursor = profiler.Buffer.LogWriteOffset;
                });
                for (var step = 0; step < 40; step++)
                {
                    if (phase is "boundary" or "exploration")
                    {
                        var x = phase == "boundary" ? 1000f + (step % 2 == 0 ? -0.1f : 0.1f) : 1000f + step * 2f;
                        await Server.WaitPost(() => transform.SetWorldPosition(viewer, new Vector2(x, 1000.1f)));
                    }
                    await Server.WaitRunTicks(6);
                    await Server.WaitPost(() =>
                    {
                        var buffer = profiler.Buffer;
                        for (var i = Math.Max(cursor, buffer.LogWriteOffset - buffer.LogBuffer.Length); i < buffer.LogWriteOffset; i++)
                        {
                            var entry = buffer.Log(i);
                            if (entry.Type != ProfLogType.Value || entry.Value.Value.Type != ProfValueType.TimeAllocSample)
                                continue;
                            var name = profiler.GetString(entry.Value.StringId);
                            if (!samples.TryGetValue(name, out var list))
                                samples[name] = list = new List<TimeAndAllocSample>();
                            list.Add(entry.Value.Value.TimeAllocSample);
                        }
                        cursor = buffer.LogWriteOffset;
                    });
                }

                foreach (var (name, values) in samples.OrderByDescending(pair => pair.Value.Sum(value => value.Time)).Take(12))
                {
                    var times = values.Select(value => value.Time * 1000d).Order().ToArray();
                    TestContext.Out.WriteLine($"PROFILE {phase} {name}: n={times.Length} mean_ms={times.Average():F3} p95_ms={times[(int) ((times.Length - 1) * .95)]:F3} max_ms={times[^1]:F3} alloc_KiB={values.Sum(value => value.Alloc) / 1024d:F1}");
                }
                await Server.WaitPost(() =>
                {
                    var levels = em.GetComponent<ClassicZMapNetworkComponent>(network);
                    foreach (var (depth, uid) in levels.ZLevels)
                    {
                        if (uid is { } grid && em.TryGetComponent<BiomeComponent>(grid, out var biome))
                            TestContext.Out.WriteLine($"TERRAIN {phase} z={depth} chunks={biome.LoadedChunks.Count} entities={biome.LoadedEntities.Values.Sum(chunk => chunk.Count)} modified={biome.ModifiedTiles.Values.Sum(chunk => chunk.Count)}");
                    }
                });
            }
        }
        finally
        {
            await Server.RemoveDummySession(sessions[0]);
            if (network != EntityUid.Invalid)
            {
                await Server.WaitPost(() => zLevels.DeleteMapNetwork(network));
                await Server.WaitRunTicks(2);
            }
        }
    }
}
