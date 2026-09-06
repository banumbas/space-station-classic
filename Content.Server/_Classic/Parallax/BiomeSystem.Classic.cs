// Namespace does not match folder structure
#pragma warning disable IDE0130
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Server._Classic.Geyser;
using Content.Server._Classic.Station;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Tag;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Parallax;

/// <summary>
/// Classic-specific biome streaming fast paths.
/// </summary>
public sealed partial class BiomeSystem
{
    [Dependency] private EntityQuery<ClassicUndergroundBiomeComponent> _classicUndergroundQuery = default!;
    [Dependency] private EntityQuery<ClassicBiomeAlwaysUnloadComponent> _classicAlwaysUnloadQuery = default!;
    [Dependency] private EntityQuery<ClassicGeyserGeneratorComponent> _classicGeyserGeneratorQuery = default!;
    [Dependency] private EntityQuery<ClassicBiomeStreamingComponent> _classicStreamingQuery = default!;
    [Dependency] private IGameTiming _classicTiming = default!;
    [Dependency] private ISerializationManager _classicSerialization = default!;
    [Dependency] private EntityQuery<ClassicZMapComponent> _classicZMapQuery = default!;
    [Dependency] private ClassicZLevelsSystem _classicZLevels = default!;
    [Dependency] private EntityQuery<DamageableComponent> _classicDamageableQuery = default!;

    private readonly HashSet<EntityUid> _classicBiomeTileWrites = new();
    private readonly Dictionary<(EntityPrototype Prototype, int Depth), ClassicZPhysicsComponent> _classicZPhysicsDefaults = new();

    private void InitializeClassicBiome()
    {
        // Landing cells must exist before Z-physics tests the surface on an adjacent map.
        UpdatesBefore.Add(typeof(ClassicZLevelsSystem));
        SubscribeLocalEvent<BiomeComponent, TileChangedEvent>(OnClassicBiomeTileChanged);
    }

    private void LoadClassicStreamingChunks(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        ClassicBiomeStreamingComponent streaming)
    {
        streaming.WorkStarted = Stopwatch.GetTimestamp();
        var active = _activeChunks[biome];
        streaming.PendingLoads.Clear();
        foreach (var chunk in active)
        {
            LoadChunkMarkers(biome, gridUid, grid, chunk, seed);
            if (biome.LoadedChunks.Contains(chunk))
                continue;

            if (streaming.ViewerChunks.Contains(chunk))
            {
                biome.LoadedChunks.Add(chunk);
                LoadChunk(biome, gridUid, grid, chunk, seed);
                continue;
            }

            var distance = float.MaxValue;
            foreach (var viewer in streaming.ViewerChunks)
            {
                var delta = chunk - viewer;
                distance = MathF.Min(distance, (float) delta.X * delta.X + (float) delta.Y * delta.Y);
            }
            streaming.PendingLoads.Add((chunk, distance));
        }

        streaming.PendingLoads.Sort(static (a, b) =>
        {
            var result = a.Distance.CompareTo(b.Distance);
            if (result != 0)
                return result;
            result = a.Chunk.X.CompareTo(b.Chunk.X);
            return result != 0 ? result : a.Chunk.Y.CompareTo(b.Chunk.Y);
        });

        var count = Math.Min(Math.Max(0, streaming.BackgroundChunksPerTick), streaming.PendingLoads.Count);
        for (var i = 0; i < count; i++)
        {
            if (ClassicStreamingBudgetExpired(streaming))
                break;

            var chunk = streaming.PendingLoads[i].Chunk;
            biome.LoadedChunks.Add(chunk);
            LoadChunk(biome, gridUid, grid, chunk, seed);
        }
        streaming.PendingLoads.Clear();
    }

    private static bool ClassicStreamingBudgetExpired(ClassicBiomeStreamingComponent streaming)
    {
        return Stopwatch.GetElapsedTime(streaming.WorkStarted) >= streaming.WorkBudget;
    }

    private Vector2 ClassicBiomeViewerPosition(EntityUid? attached, EntityUid viewer, TransformComponent xform)
    {
        // Z-eyes follow their owner only once per second for PVS. Terrain must follow the
        // current position so the adjacent landing chunk exists even after a same-map teleport.
        if (_classicZLevels.IsViewerEye(attached, viewer) &&
            _xformQuery.TryGetComponent(attached, out var ownerXform))
        {
            return _transform.GetWorldPosition(ownerXform);
        }

        return _transform.GetWorldPosition(xform);
    }

    private void OnClassicBiomeTileChanged(Entity<BiomeComponent> ent, ref TileChangedEvent args)
    {
        if (_classicBiomeTileWrites.Contains(ent.Owner))
            return;

        foreach (var change in args.Changes)
        {
            var chunkOrigin = SharedMapSystem.GetChunkIndices(change.GridIndices, ChunkSize) * ChunkSize;
            ent.Comp.ModifiedTiles.GetOrNew(chunkOrigin).Add(change.GridIndices);
        }
    }

    private void SetClassicBiomeTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices,
        Tile tile)
    {
        _classicBiomeTileWrites.Add(gridUid);
        try
        {
            _mapSystem.SetTile(gridUid, grid, indices, tile);
        }
        finally
        {
            _classicBiomeTileWrites.Remove(gridUid);
        }
    }

    private void SetClassicBiomeTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        List<(Vector2i Index, Tile Tile)> tiles)
    {
        _classicBiomeTileWrites.Add(gridUid);
        try
        {
            _mapSystem.SetTiles(gridUid, grid, tiles);
        }
        finally
        {
            _classicBiomeTileWrites.Remove(gridUid);
        }
    }

    private bool ClassicBiomeLoadsEntities(EntityUid gridUid)
    {
        return !_classicUndergroundQuery.TryComp(gridUid, out var underground) || underground.LoadEntities;
    }

    private bool ClassicBiomeLoadsDecals(EntityUid gridUid)
    {
        return !_classicUndergroundQuery.TryComp(gridUid, out var underground) || underground.LoadDecals;
    }

    private bool ClassicCanUnloadBiomeEntity(EntityUid gridUid, EntityUid uid)
    {
        if (_classicAlwaysUnloadQuery.HasComponent(uid))
            return true;

        if (!_classicStreamingQuery.HasComp(gridUid))
            return EntityManager.IsDefault(uid);

        // Damage is a read-only serialized field and is omitted by data-field equality.
        // It must be checked explicitly so streaming never heals a mined/damaged rock.
        if (_classicDamageableQuery.TryComp(uid, out var damageable) && damageable.TotalDamage != 0)
            return false;

        var metadata = MetaData(uid);
        if (metadata.EntityPrototype is not { } prototype ||
            metadata.EntityName != prototype.Name || metadata.EntityDescription != prototype.Description)
            return false;

        // Transform can itself be declared on a prototype. Counting it again as an implicit
        // component makes pristine anchored rocks fail EntityManager.IsDefault's count check.
        var remaining = prototype.Components.Count;
        if (prototype.Components.ContainsKey("Transform"))
            remaining--;
        if (prototype.Components.ContainsKey("MetaData"))
            remaining--;

        foreach (var component in AllComps(uid))
        {
            if (component.Deleted)
                return false;
            if (component is TransformComponent or MetaDataComponent)
                continue;

            var type = component.GetType();
            var name = EntityManager.ComponentFactory.GetRegistration(type).Name;
            if (!prototype.Components.TryGetValue(name, out var entry))
                return false;
            remaining--;

            // Compare tag contents, rather than their collection identity.
            if (component is TagComponent tags && entry.Component is TagComponent prototypeTags)
            {
                if (tags.Tags.Count != prototypeTags.Tags.Count || !_tags.HasAllTags(tags, prototypeTags.Tags))
                    return false;
                continue;
            }

            var expected = entry.Component;
            if (component is ClassicZPhysicsComponent && expected is ClassicZPhysicsComponent prototypePhysics)
            {
                var depth = _classicZMapQuery.TryComp(Transform(uid).MapUid, out var zMap) ? zMap.Depth : 0;
                var key = (prototype, depth);
                if (!_classicZPhysicsDefaults.TryGetValue(key, out var physics))
                {
                    physics = _classicSerialization.CreateCopy(prototypePhysics, notNullableOverride: true);
                    physics.CurrentZLevel = depth;
                    _classicZPhysicsDefaults[key] = physics;
                }
                expected = physics;
            }

            // Damage, construction, added/removed components and other persistent changes must
            // still prevent unloading. Only the derived Z-map depth differs from the prototype.
            if (!_classicSerialization.DataFieldEquals(type, component, expected))
                return false;
        }

        return remaining == 0;
    }

    private bool ClassicHasPersistentBiomeDependent(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        EntityUid biomeEntity)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (other.Value != biomeEntity && _classicGeyserGeneratorQuery.HasComp(other.Value))
                return true;
        }

        return false;
    }

    private bool TryGetActiveBiome(TransformComponent xform, [NotNullWhen(true)] out BiomeComponent? biome)
    {
        // Underground terrain maps deliberately use one entity as both Map and MapGrid. A Z-eye
        // parented directly to that map therefore resolves the biome without scanning every grid.
        if (xform.MapUid is { } mapUid && _biomeQuery.TryGetComponent(mapUid, out biome))
            return true;

        if (xform.GridUid is { } gridUid && _biomeQuery.TryGetComponent(gridUid, out biome))
            return true;

        biome = null;
        return false;
    }
}
