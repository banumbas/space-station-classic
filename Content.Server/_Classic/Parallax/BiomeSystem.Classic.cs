// Namespace does not match folder structure
#pragma warning disable IDE0130
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Server._Classic.Geyser;
using Content.Server._Classic.Station;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Content.Server._Classic.ZLevels.Core;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Profiling;
using Robust.Shared.Serialization.Manager;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

/// <summary>
/// Classic-specific biome streaming fast paths.
/// </summary>
public sealed partial class BiomeSystem
{
    [Dependency] private EntityQuery<ClassicUndergroundBiomeComponent> _classicUndergroundQuery = default!;
    [Dependency] private EntityQuery<ClassicBiomeAlwaysUnloadComponent> _classicAlwaysUnloadQuery = default!;
    [Dependency] private EntityQuery<ClassicGeyserGeneratorComponent> _classicGeyserGeneratorQuery = default!;
    [Dependency] private EntityQuery<ClassicGeyserOutletComponent> _classicGeyserOutletQuery = default!;
    [Dependency] private EntityQuery<ClassicBiomeStreamingComponent> _classicStreamingQuery = default!;
    [Dependency] private IGameTiming _classicTiming = default!;
    [Dependency] private ISerializationManager _classicSerialization = default!;
    [Dependency] private EntityQuery<ClassicZMapComponent> _classicZMapQuery = default!;
    [Dependency] private EntityQuery<ClassicZPhysicsComponent> _classicZPhysicsQuery = default!;
    [Dependency] private EntityQuery<ClassicZLevelHighGroundComponent> _classicHighGroundQuery = default!;
    [Dependency] private ClassicZLevelsSystem _classicZLevels = default!;
    [Dependency] private EntityQuery<DamageableComponent> _classicDamageableQuery = default!;
    [Dependency] private ProfManager _classicProfiler = default!;

    private readonly HashSet<EntityUid> _classicBiomeTileWrites = new();
    private readonly Dictionary<(EntityPrototype Prototype, int Depth), ClassicZPhysicsComponent> _classicZPhysicsDefaults = new();
    private readonly List<(EntityUid GridUid, BiomeComponent Biome, MapGridComponent Grid, ClassicBiomeStreamingComponent Streaming)> _classicStreamingBiomes = new();
    private readonly Dictionary<Vector2i, ulong> _classicViewerCellMasks = new();
    private readonly List<Vector2i> _classicViewerCellChunks = new();
    private readonly List<Vector2i> _classicStaleUnloadChunks = new();
    private readonly HashSet<Vector2i> _classicRearmedUnloadChunks = new();
    private readonly ClassicZLevelOpeningCache _classicOpeningCache = new();
    private readonly List<Entity<MapGridComponent>> _classicOpeningGrids = new();
    private readonly List<Box2> _classicOpeningBounds = new();
    private readonly List<ClassicBiomeOpeningScanKey> _classicStaleOpeningScans = new();
    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private readonly Tile?[] _chunkBiomeTiles = new Tile?[ChunkSize * ChunkSize];
    private readonly List<Vector2i> _unloadChunks = new();
    private readonly List<(Vector2i, Tile)> _unloadTiles = new(ChunkSize * ChunkSize);
    private readonly List<(EntityUid Entity, Vector2i Tile)> _classicUnloadEntities = new(ChunkSize * ChunkSize);
    private readonly List<Vector2i> _classicRestoreCells = new(ChunkSize * ChunkSize);
    private readonly List<string> _classicMarkerLayers = new();
    private float _classicLoadRange = DefaultLoadRange;
    private const int MaxClassicBackgroundChunksPerTick = 64;
    private const int ClassicChunkCells = ChunkSize * ChunkSize;
    private long _classicStreamingWorkStarted;
    private TimeSpan _classicStreamingWorkBudget;
    private int _classicStreamingStartIndex;
    private bool _classicForcedProgressUsed;
    private bool _classicUnloadFirst;

    private void BeginClassicStreamingBudget(TimeSpan budget)
    {
        _classicStreamingWorkBudget = budget < TimeSpan.Zero ? TimeSpan.Zero : budget;
        _classicStreamingWorkStarted = Stopwatch.GetTimestamp();
        _classicForcedProgressUsed = false;
    }

    private void PruneClassicOpeningScans(ClassicBiomeStreamingComponent streaming)
    {
        _classicStaleOpeningScans.Clear();
        foreach (var (key, scan) in streaming.OpeningScans)
        {
            if (scan.LastRequestGeneration != streaming.OpeningScanGeneration &&
                _classicTiming.CurTime - scan.LastRequestTime >= streaming.OpeningCacheDuration)
            {
                _classicStaleOpeningScans.Add(key);
            }
        }

        foreach (var key in _classicStaleOpeningScans)
            streaming.OpeningScans.Remove(key);

        var maxEntries = Math.Clamp(streaming.MaxOpeningCacheEntries, 1, 128);
        while (streaming.OpeningScans.Count > maxEntries)
        {
            var found = false;
            var oldestKey = default(ClassicBiomeOpeningScanKey);
            var oldestTime = TimeSpan.MaxValue;
            foreach (var (key, scan) in streaming.OpeningScans)
            {
                if (scan.LastRequestGeneration == streaming.OpeningScanGeneration ||
                    found && scan.LastRequestTime >= oldestTime)
                {
                    continue;
                }

                found = true;
                oldestKey = key;
                oldestTime = scan.LastRequestTime;
            }

            if (!found)
                break;
            streaming.OpeningScans.Remove(oldestKey);
        }
    }

    private void InitializeClassicBiome()
    {
        _eyeQuery = GetEntityQuery<EyeComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        Subs.CVar(_configManager, CVars.NetMaxUpdateRange, SetClassicLoadRange, true);
        Subs.CVar(_configManager, CVars.NetPvsPriorityRange, SetClassicLoadRange, true);
        UpdatesBefore.Add(typeof(ClassicZLevelsSystem));
        UpdatesBefore.Add(typeof(Robust.Server.GameObjects.PhysicsSystem));
        SubscribeLocalEvent<TileChangedEvent>(OnClassicTileChanged);
        SubscribeLocalEvent<GridRemovalEvent>(OnClassicOpeningGridRemoved);
    }

    private void SetClassicLoadRange(float _)
    {
        _classicLoadRange = Math.Max(
            16f,
            Math.Max(
                _configManager.GetCVar(CVars.NetMaxUpdateRange),
                _configManager.GetCVar(CVars.NetPvsPriorityRange))) / 2f;
    }

    private void ClearClassicPrototypeCaches()
    {
        _classicOpeningCache.Clear();
    }

    private void OnClassicOpeningGridRemoved(GridRemovalEvent args)
    {
        _classicOpeningCache.RemoveGrid(args.EntityUid);
    }

    private void LoadClassicViewerChunks(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        ClassicBiomeStreamingComponent streaming)
    {
        _classicViewerCellMasks.Clear();
        foreach (var cell in streaming.ViewerCells)
        {
            var chunk = SharedMapSystem.GetChunkIndices(cell, ChunkSize) * ChunkSize;
            var local = cell - chunk;
            var cellIndex = local.X * ChunkSize + local.Y;
            var bit = 1UL << cellIndex;

            if (streaming.PartialUnloads.TryGetValue(chunk, out var unloading))
            {
                unloading.Phase = ClassicBiomePartialUnloadPhase.Restoring;
                RestoreClassicUnload(
                    biome,
                    gridUid,
                    grid,
                    chunk,
                    seed,
                    streaming,
                    unloading,
                    1,
                    cell);

                if (streaming.PartialUnloads.ContainsKey(chunk))
                {
                    if (unloading.WasPartial &&
                        unloading.PartialCursor <= cellIndex &&
                        (unloading.PriorityCells & bit) == 0)
                    {
                        using (_classicProfiler.Value("ClassicBiome.UrgentLoad"))
                            LoadChunkCells(biome, gridUid, grid, chunk, seed, cellIndex, 1);
                        unloading.PriorityCells |= bit;
                        unloading.MaterializedCells |= bit;
                    }

                    continue;
                }
            }

            if (biome.LoadedChunks.Contains(chunk))
            {
                streaming.PartialLoads.Remove(chunk);
                streaming.PriorityLoadedCells.Remove(chunk);
                streaming.MaterializedCells.Remove(chunk);
                continue;
            }

            streaming.PartialLoads.TryAdd(chunk, 0);
            var generated = streaming.PriorityLoadedCells.GetValueOrDefault(chunk);
            if (streaming.PartialLoads[chunk] > cellIndex || (generated & bit) != 0)
                continue;

            _classicViewerCellMasks[chunk] = _classicViewerCellMasks.GetValueOrDefault(chunk) | bit;
        }

        _classicViewerCellChunks.Clear();
        _classicViewerCellChunks.AddRange(_classicViewerCellMasks.Keys);
        _classicViewerCellChunks.Sort(CompareClassicChunks);
        if (_classicViewerCellChunks.Count == 0)
            return;

        using (_classicProfiler.Value("ClassicBiome.UrgentBatch"))
        {
            foreach (var chunk in _classicViewerCellChunks)
            {
                var pending = _classicViewerCellMasks[chunk];
                var generated = streaming.PriorityLoadedCells.GetValueOrDefault(chunk);
                while (pending != 0)
                {
                    var start = BitOperations.TrailingZeroCount(pending);
                    var end = start + 1;
                    while (end < ClassicChunkCells && (pending & (1UL << end)) != 0)
                        end++;

                    using (_classicProfiler.Value("ClassicBiome.UrgentLoad"))
                        LoadChunkCells(biome, gridUid, grid, chunk, seed, start, end - start);

                    var length = end - start;
                    var run = length == ClassicChunkCells
                        ? ulong.MaxValue
                        : ((1UL << length) - 1UL) << start;
                    generated |= run;
                    pending &= ~run;
                }

                streaming.PriorityLoadedCells[chunk] = generated;
                streaming.MaterializedCells[chunk] =
                    streaming.MaterializedCells.GetValueOrDefault(chunk) | generated;
            }
        }
    }

    /// <summary>
    /// Advances a Classic chunk teardown by one bounded operation. Entity comparison and entity
    /// deletion are the expensive part and are sliced; decal/tile finalization is kept as its own
    /// operation so it cannot be added to the last entity slice in the same tick.
    /// </summary>
    private void UnloadClassicChunks(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        ClassicBiomeStreamingComponent streaming)
    {
        var active = _activeChunks[biome];
        _classicRearmedUnloadChunks.Clear();
        PruneClassicZLevelCache(streaming);

        using (_classicProfiler.Value("ClassicBiome.UnloadDiscover"))
        {
            if (!streaming.UnloadTrackingInitialized)
            {
                foreach (var chunk in biome.LoadedChunks)
                {
                    if (!IsClassicChunkRetained(active, streaming, chunk))
                        streaming.PendingUnloads.TryAdd(chunk, GetClassicUnloadDeadline(streaming, chunk));
                }

                foreach (var chunk in streaming.PartialLoads.Keys)
                {
                    if (!IsClassicChunkRetained(active, streaming, chunk))
                        streaming.PendingUnloads.TryAdd(chunk, GetClassicUnloadDeadline(streaming, chunk));
                }

                streaming.UnloadTrackingInitialized = true;
            }
            else
            {
                foreach (var chunk in streaming.PreviousActiveChunks)
                {
                    if (IsClassicChunkRetained(active, streaming, chunk) ||
                        !biome.LoadedChunks.Contains(chunk) && !streaming.PartialLoads.ContainsKey(chunk))
                    {
                        continue;
                    }

                    streaming.PendingUnloads.TryAdd(chunk, GetClassicUnloadDeadline(streaming, chunk));
                }
            }

            foreach (var chunk in active)
            {
                streaming.PendingUnloads.Remove(chunk);
                streaming.CachedZChunks.Remove(chunk);
                if (streaming.PartialUnloads.TryGetValue(chunk, out var partial))
                    partial.Phase = ClassicBiomePartialUnloadPhase.Restoring;
            }
            foreach (var chunk in streaming.LandingChunks)
            {
                streaming.PendingUnloads.Remove(chunk);
                if (streaming.PartialUnloads.TryGetValue(chunk, out var partial))
                    partial.Phase = ClassicBiomePartialUnloadPhase.Restoring;
            }

            streaming.PreviousActiveChunks.Clear();
            streaming.PreviousActiveChunks.UnionWith(active);
            streaming.PreviousActiveChunks.UnionWith(streaming.LandingChunks);
        }

        var configuredOperations = Math.Clamp(
            streaming.BackgroundChunksPerTick,
            0,
            MaxClassicBackgroundChunksPerTick);
        var operations = Math.Max(1, configuredOperations);

        for (var operation = 0; operation < operations; operation++)
        {
            Vector2i chunk;
            ClassicBiomePartialUnloadState? state;
            if (ClassicStreamingBudgetExpired())
            {
                if (_classicForcedProgressUsed)
                {
                    break;
                }

                if (!TryGetClassicPartialUnload(streaming, out chunk, out state) &&
                    !TryStartClassicUnload(biome, active, streaming, out chunk, out state))
                {
                    break;
                }

                if (!TryBeginClassicBoundedOperation(true))
                    break;
            }
            else
            {
                var hadPartial = TryGetClassicPartialUnload(streaming, out chunk, out state);
                if (!hadPartial)
                {
                    if (!TryBeginClassicBoundedOperation(false) ||
                        !TryStartClassicUnload(biome, active, streaming, out chunk, out state))
                    {
                        break;
                    }
                }
                else if (!TryBeginClassicBoundedOperation(true))
                {
                    break;
                }
            }

            var currentState = state!;
            switch (currentState.Phase)
            {
                case ClassicBiomePartialUnloadPhase.Entities:
                    using (_classicProfiler.Value("ClassicBiome.UnloadEntitySlice"))
                    {
                        UnloadClassicEntitySlice(
                            biome,
                            gridUid,
                            grid,
                            chunk,
                            streaming,
                            currentState,
                            Math.Clamp(streaming.UnloadEntitiesPerSlice, 1, ClassicChunkCells));
                    }
                    break;
                case ClassicBiomePartialUnloadPhase.Finalize:
                    if (IsClassicChunkRetained(active, streaming, chunk))
                    {
                        currentState.Phase = ClassicBiomePartialUnloadPhase.Restoring;
                        goto case ClassicBiomePartialUnloadPhase.Restoring;
                    }

                    using (_classicProfiler.Value("ClassicBiome.UnloadFinalize"))
                    {
                        AdvanceClassicUnloadFinalize(
                            biome,
                            gridUid,
                            grid,
                            chunk,
                            seed,
                            currentState,
                            Math.Clamp(streaming.UnloadEntitiesPerSlice, 1, ClassicChunkCells));
                    }
                    break;
                case ClassicBiomePartialUnloadPhase.Commit:
                    if (IsClassicChunkRetained(active, streaming, chunk))
                    {
                        currentState.Phase = ClassicBiomePartialUnloadPhase.Restoring;
                        goto case ClassicBiomePartialUnloadPhase.Restoring;
                    }

                    using (_classicProfiler.Value("ClassicBiome.UnloadCommit"))
                    {
                        CommitClassicUnload(biome, gridUid, grid, chunk, streaming, currentState);
                    }
                    break;
                case ClassicBiomePartialUnloadPhase.Restoring:
                    using (_classicProfiler.Value("ClassicBiome.UnloadRestore"))
                    {
                        RestoreClassicUnload(
                            biome,
                            gridUid,
                            grid,
                            chunk,
                            seed,
                            streaming,
                            currentState,
                            Math.Clamp(streaming.UnloadEntitiesPerSlice, 1, ClassicChunkCells));
                    }
                    break;
            }

            if (ClassicStreamingBudgetExpired())
            {
                _classicForcedProgressUsed = true;
                break;
            }
        }
    }

    private bool TryStartClassicUnload(
        BiomeComponent biome,
        HashSet<Vector2i> active,
        ClassicBiomeStreamingComponent streaming,
        out Vector2i chunk,
        [NotNullWhen(true)] out ClassicBiomePartialUnloadState? state)
    {
        chunk = default;
        state = null;
        var found = false;
        var foundPartial = false;

        _classicStaleUnloadChunks.Clear();
        foreach (var (candidate, deadline) in streaming.PendingUnloads)
        {
            if (_classicRearmedUnloadChunks.Contains(candidate))
                continue;

            if (IsClassicChunkRetained(active, streaming, candidate))
            {
                _classicStaleUnloadChunks.Add(candidate);
                continue;
            }

            var partial = streaming.PartialLoads.ContainsKey(candidate);
            if (!partial && !biome.LoadedChunks.Contains(candidate))
            {
                _classicStaleUnloadChunks.Add(candidate);
                continue;
            }

            if (_classicTiming.CurTime < deadline)
                continue;

            chunk = candidate;
            found = true;
            foundPartial = partial;
            break;
        }

        foreach (var stale in _classicStaleUnloadChunks)
            streaming.PendingUnloads.Remove(stale);

        if (!found)
            return false;

        var partialCursor = 0;
        var priorityCells = 0UL;
        var materializedCells = ulong.MaxValue;
        if (foundPartial)
        {
            if (!streaming.PartialLoads.Remove(chunk, out partialCursor))
                return false;
            streaming.PriorityLoadedCells.Remove(chunk, out priorityCells);
            streaming.MaterializedCells.Remove(chunk, out materializedCells);
            materializedCells |= ClassicCellMask(0, partialCursor) | priorityCells;
        }
        else if (!biome.LoadedChunks.Remove(chunk))
        {
            return false;
        }

        streaming.PendingUnloads.Remove(chunk);
        state = new ClassicBiomePartialUnloadState
        {
            WasPartial = foundPartial,
            PartialCursor = Math.Clamp(partialCursor, 0, ClassicChunkCells),
            PriorityCells = priorityCells,
            MaterializedCells = materializedCells,
        };
        streaming.PartialUnloads.Add(chunk, state);
        return true;
    }

    private static bool TryGetClassicPartialUnload(
        ClassicBiomeStreamingComponent streaming,
        out Vector2i chunk,
        [NotNullWhen(true)] out ClassicBiomePartialUnloadState? state)
    {
        chunk = default;
        state = null;
        var found = false;
        var foundRestoring = false;

        foreach (var (candidate, candidateState) in streaming.PartialUnloads)
        {
            var restoring = candidateState.Phase == ClassicBiomePartialUnloadPhase.Restoring;
            if (found && (foundRestoring && !restoring ||
                foundRestoring == restoring && CompareClassicChunks(candidate, chunk) >= 0))
            {
                continue;
            }

            chunk = candidate;
            state = candidateState;
            found = true;
            foundRestoring = restoring;
        }

        return found;
    }

    private void UnloadClassicEntitySlice(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        ClassicBiomeStreamingComponent streaming,
        ClassicBiomePartialUnloadState state,
        int limit)
    {
        if (!biome.LoadedEntities.TryGetValue(chunk, out var loaded) || loaded.Count == 0)
        {
            biome.LoadedEntities.Remove(chunk);
            state.Phase = ClassicBiomePartialUnloadPhase.Finalize;
            return;
        }

        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _classicUnloadEntities.Clear();
        foreach (var pair in loaded)
        {
            _classicUnloadEntities.Add((pair.Key, pair.Value));
            if (_classicUnloadEntities.Count >= limit)
                break;
        }

        var depth = ClassicBiomeDepth(gridUid);
        foreach (var (entity, tile) in _classicUnloadEntities)
        {
            if (Deleted(entity) || !_xformQuery.TryGetComponent(entity, out var xform))
            {
                modified.Add(tile);
            }
            else
            {
                var entityTile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);
                if (!xform.Anchored || entityTile != tile ||
                    ClassicHasPersistentBiomeDependent(gridUid, grid, tile, entity) ||
                    !ClassicCanUnloadBiomeEntity(gridUid, entity, depth))
                {
                    modified.Add(tile);
                }
                else
                {
                    Del(entity);
                    state.RemovedEntityCells.Add(tile);
                }
            }

            loaded.Remove(entity);

            if (ClassicStreamingBudgetExpired())
                break;
        }

        if (loaded.Count == 0)
        {
            biome.LoadedEntities.Remove(chunk);
            state.Phase = ClassicBiomePartialUnloadPhase.Finalize;
        }

        StoreClassicModifiedTiles(biome, chunk, modified);
    }

    private void AdvanceClassicUnloadFinalize(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        ClassicBiomePartialUnloadState state,
        int limit)
    {
        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        var first = Math.Clamp(state.FinalizeCursor, 0, ClassicChunkCells);
        var end = Math.Min(ClassicChunkCells, first + Math.Clamp(limit, 1, ClassicChunkCells));
        for (var cell = first; cell < end; cell++)
        {
            if (cell > first && ClassicStreamingBudgetExpired())
                break;

            state.FinalizeCursor = cell + 1;
            if ((state.MaterializedCells & (1UL << cell)) == 0)
                continue;

            var x = cell / ChunkSize;
            var y = cell % ChunkSize;
            var indices = new Vector2i(x + chunk.X, y + chunk.Y);
            if (modified.Contains(indices))
                continue;

            var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
            if (anchored.MoveNext(out _))
            {
                modified.Add(indices);
                continue;
            }

            if (!TryGetTile(indices, biome.Layers, seed, (Entity<MapGridComponent>?) null, out var biomeTile) ||
                _mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) &&
                tileRef.Tile != biomeTile.Value)
            {
                modified.Add(indices);
                continue;
            }

            state.ClearableCells |= 1UL << cell;

            if (ClassicStreamingBudgetExpired())
                break;
        }

        StoreClassicModifiedTiles(biome, chunk, modified);
        if (state.FinalizeCursor >= ClassicChunkCells)
            state.Phase = ClassicBiomePartialUnloadPhase.Commit;
    }

    private void CommitClassicUnload(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        ClassicBiomeStreamingComponent streaming,
        ClassicBiomePartialUnloadState state)
    {
        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        if (biome.LoadedDecals.Remove(chunk, out var decals))
        {
            foreach (var (decal, indices) in decals)
            {
                if (!_decals.RemoveDecal(gridUid, decal))
                    modified.Add(indices);
            }
        }

        _unloadTiles.Clear();
        for (var cell = 0; cell < ClassicChunkCells; cell++)
        {
            if ((state.ClearableCells & (1UL << cell)) == 0)
                continue;

            var x = cell / ChunkSize;
            var y = cell % ChunkSize;
            var indices = new Vector2i(x + chunk.X, y + chunk.Y);
            if (modified.Contains(indices))
                continue;

            var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
            if (anchored.MoveNext(out _))
            {
                modified.Add(indices);
                continue;
            }

            _unloadTiles.Add((indices, Tile.Empty));
        }

        SetClassicBiomeTiles(gridUid, grid, _unloadTiles);
        _unloadTiles.Clear();
        streaming.PartialUnloads.Remove(chunk);
        streaming.PendingUnloads.Remove(chunk);
        StoreClassicModifiedTiles(biome, chunk, modified);
    }

    private void RestoreClassicUnload(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        ClassicBiomeStreamingComponent streaming,
        ClassicBiomePartialUnloadState state,
        int limit,
        Vector2i? priorityCell = null)
    {
        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _classicRestoreCells.Clear();
        if (priorityCell is { } requested)
        {
            if (state.RemovedEntityCells.Contains(requested))
                _classicRestoreCells.Add(requested);
        }
        else
        {
            foreach (var tile in state.RemovedEntityCells)
                _classicRestoreCells.Add(tile);
            _classicRestoreCells.Sort(CompareClassicChunks);
            if (_classicRestoreCells.Count > limit)
                _classicRestoreCells.RemoveRange(limit, _classicRestoreCells.Count - limit);
        }

        biome.LoadedEntities.TryGetValue(chunk, out var loaded);
        foreach (var tile in _classicRestoreCells)
        {
            if (!modified.Contains(tile))
            {
                var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
                if (anchored.MoveNext(out _))
                {
                    modified.Add(tile);
                }
                else if (TryGetEntity(tile, biome, (gridUid, grid), out var prototype))
                {
                    var entity = Spawn(prototype, _mapSystem.GridTileToLocal(gridUid, grid, tile));
                    if (_xformQuery.TryGetComponent(entity, out var xform) && !xform.Anchored)
                        _transform.AnchorEntity((entity, xform), (gridUid, grid), tile);

                    loaded ??= new Dictionary<EntityUid, Vector2i>(ChunkSize * ChunkSize);
                    loaded.Add(entity, tile);
                }
            }

            state.RemovedEntityCells.Remove(tile);

            if (ClassicStreamingBudgetExpired())
                break;
        }

        if (loaded is { Count: > 0 })
            biome.LoadedEntities[chunk] = loaded;

        StoreClassicModifiedTiles(biome, chunk, modified);
        if (state.RemovedEntityCells.Count > 0)
            return;

        streaming.PartialUnloads.Remove(chunk);
        streaming.PartialLoads.Remove(chunk);
        streaming.PriorityLoadedCells.Remove(chunk);
        streaming.MaterializedCells.Remove(chunk);
        if (IsClassicChunkRetained(_activeChunks[biome], streaming, chunk))
        {
            streaming.PendingUnloads.Remove(chunk);
        }
        else
        {
            streaming.PendingUnloads.TryAdd(chunk, GetClassicUnloadDeadline(streaming, chunk));
            _classicRearmedUnloadChunks.Add(chunk);
        }

        if (!state.WasPartial || state.PartialCursor >= ClassicChunkCells)
        {
            biome.LoadedChunks.Add(chunk);
            return;
        }

        var cursor = Math.Clamp(state.PartialCursor, 0, ClassicChunkCells);
        streaming.PartialLoads[chunk] = cursor;
        var sequential = cursor == 0 ? 0UL : (1UL << cursor) - 1UL;
        var priority = state.PriorityCells & ~sequential;
        if (priority != 0)
            streaming.PriorityLoadedCells[chunk] = priority;

        var materialized = state.MaterializedCells & ~sequential;
        if (materialized != 0 || cursor != 0)
            streaming.MaterializedCells[chunk] = materialized | sequential;
    }

    private void StoreClassicModifiedTiles(BiomeComponent biome, Vector2i chunk, HashSet<Vector2i> modified)
    {
        if (modified.Count > 0)
        {
            biome.ModifiedTiles[chunk] = modified;
            return;
        }

        biome.ModifiedTiles.Remove(chunk);
        _tilePool.Return(modified);
    }

    private static int CompareClassicChunks(Vector2i a, Vector2i b)
    {
        var result = a.X.CompareTo(b.X);
        return result != 0 ? result : a.Y.CompareTo(b.Y);
    }

    private static bool IsClassicChunkRetained(
        HashSet<Vector2i> active,
        ClassicBiomeStreamingComponent streaming,
        Vector2i chunk)
    {
        return active.Contains(chunk) || streaming.LandingChunks.Contains(chunk);
    }

    private static ulong ClassicCellMask(int start, int end)
    {
        start = Math.Clamp(start, 0, ClassicChunkCells);
        end = Math.Clamp(end, start, ClassicChunkCells);
        var belowEnd = end == ClassicChunkCells ? ulong.MaxValue : (1UL << end) - 1UL;
        var belowStart = start == 0 ? 0UL : (1UL << start) - 1UL;
        return belowEnd & ~belowStart;
    }

    private void LoadClassicBackgroundChunks(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        ClassicBiomeStreamingComponent streaming)
    {
        var loadSlots = Math.Clamp(streaming.BackgroundChunksPerTick, 0, MaxClassicBackgroundChunksPerTick);
        if (loadSlots == 0 && streaming.PartialLoads.Count > 0)
            loadSlots = 1;
        if (loadSlots == 0)
            return;

        var cellsPerSlice = Math.Clamp(streaming.BackgroundCellsPerSlice, 1, ClassicChunkCells);
        for (var slot = 0; slot < loadSlots; slot++)
        {
            Vector2i chunk;
            bool continuation;
            if (ClassicStreamingBudgetExpired())
            {
                if (_classicForcedProgressUsed ||
                    !TrySelectClassicActivePartialLoad(biome, streaming, out chunk))
                {
                    break;
                }

                continuation = true;
            }
            else
            {
                if (!TrySelectClassicBackgroundChunk(biome, streaming, out chunk))
                    break;

                continuation = streaming.PartialLoads.ContainsKey(chunk);
            }

            if (!TryBeginClassicBoundedOperation(continuation))
                break;

            if (!continuation)
            {
                streaming.PartialLoads.Add(chunk, 0);
            }

            if (biome.LoadedChunks.Contains(chunk))
            {
                streaming.PartialLoads.Remove(chunk);
                streaming.PriorityLoadedCells.Remove(chunk);
                streaming.MaterializedCells.Remove(chunk);
                continue;
            }

            if (biome.PendingMarkers.ContainsKey(chunk))
            {
                using (_classicProfiler.Value("ClassicBiome.MarkerNodeSlice"))
                {
                    LoadChunkMarkerNodes(
                        biome,
                        gridUid,
                        grid,
                        chunk,
                        seed,
                        Math.Clamp(streaming.MarkerNodesPerSlice, 1, ClassicChunkCells));
                }

                if (ClassicStreamingBudgetExpired())
                    _classicForcedProgressUsed = true;

                continue;
            }

            var previousCell = Math.Clamp(streaming.PartialLoads[chunk], 0, ClassicChunkCells);
            var nextCell = previousCell;
            var scheduledEnd = Math.Min(ClassicChunkCells, previousCell + cellsPerSlice);
            using (_classicProfiler.Value("ClassicBiome.BackgroundSlice"))
            {
                nextCell = LoadChunkCells(
                    biome,
                    gridUid,
                    grid,
                    chunk,
                    seed,
                    nextCell,
                    cellsPerSlice,
                    stopWhenClassicBudgetExpires: true);
            }

            if (ClassicStreamingBudgetExpired())
                _classicForcedProgressUsed = true;

            streaming.MaterializedCells[chunk] =
                streaming.MaterializedCells.GetValueOrDefault(chunk) |
                ClassicCellMask(previousCell, scheduledEnd);

            if (nextCell < ClassicChunkCells)
            {
                streaming.PartialLoads[chunk] = nextCell;
                if (streaming.PriorityLoadedCells.TryGetValue(chunk, out var priority))
                {
                    var sequential = nextCell == 0 ? 0UL : (1UL << nextCell) - 1UL;
                    priority &= ~sequential;
                    if (priority == 0)
                        streaming.PriorityLoadedCells.Remove(chunk);
                    else
                        streaming.PriorityLoadedCells[chunk] = priority;
                }
                continue;
            }

            streaming.PartialLoads.Remove(chunk);
            streaming.PriorityLoadedCells.Remove(chunk);
            streaming.MaterializedCells.Remove(chunk);
            biome.LoadedChunks.Add(chunk);
        }
    }

    private void LoadClassicPendingMarkerNodes(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        ClassicBiomeStreamingComponent streaming)
    {
        var active = _activeChunks[biome];
        var found = false;
        var chunk = default(Vector2i);
        foreach (var candidate in active)
        {
            if (!biome.PendingMarkers.ContainsKey(candidate) || !biome.LoadedChunks.Contains(candidate) ||
                found && CompareClassicChunks(candidate, chunk) >= 0)
            {
                continue;
            }

            chunk = candidate;
            found = true;
        }

        if (!found)
            return;

        if (!TryBeginClassicBoundedOperation(true))
            return;

        using (_classicProfiler.Value("ClassicBiome.MarkerNodeSlice"))
        {
            LoadChunkMarkerNodes(
                biome,
                gridUid,
                grid,
                chunk,
                seed,
                Math.Clamp(streaming.MarkerNodesPerSlice, 1, ClassicChunkCells));
        }

        if (ClassicStreamingBudgetExpired())
            _classicForcedProgressUsed = true;
    }

    private bool TrySelectClassicBackgroundChunk(
        BiomeComponent biome,
        ClassicBiomeStreamingComponent streaming,
        out Vector2i chunk)
    {
        var active = _activeChunks[biome];

        chunk = default;
        var viewers = streaming.ViewerCenters;
        if (viewers.Count > 0)
        {
            var viewerIndex = Math.Abs(streaming.ViewerCursor % viewers.Count);
            var viewer = viewers[viewerIndex];
            var found = false;
            var distance = float.MaxValue;

            foreach (var candidate in active)
            {
                if (biome.LoadedChunks.Contains(candidate) ||
                    streaming.ViewerChunks.Contains(candidate) && !streaming.PartialLoads.ContainsKey(candidate) ||
                    streaming.PartialUnloads.ContainsKey(candidate))
                {
                    continue;
                }

                var delta = candidate - viewer;
                var candidateDistance = (float) delta.X * delta.X + (float) delta.Y * delta.Y;
                if (found && (candidateDistance > distance ||
                    candidateDistance.Equals(distance) && CompareClassicChunks(candidate, chunk) >= 0))
                {
                    continue;
                }

                chunk = candidate;
                distance = candidateDistance;
                found = true;
            }

            if (found)
            {
                streaming.ViewerCursor = (viewerIndex + 1) % viewers.Count;
                return true;
            }
        }

        var foundPartial = false;
        chunk = default;
        foreach (var candidate in streaming.PartialLoads.Keys)
        {
            if (!active.Contains(candidate) ||
                foundPartial && CompareClassicChunks(candidate, chunk) >= 0)
            {
                continue;
            }

            chunk = candidate;
            foundPartial = true;
        }

        return foundPartial;
    }

    /// <summary>
    /// Selects an already-started active chunk without walking the much larger active view range.
    /// This is used only for the single globally forced continuation after the shared deadline.
    /// </summary>
    private bool TrySelectClassicActivePartialLoad(
        BiomeComponent biome,
        ClassicBiomeStreamingComponent streaming,
        out Vector2i chunk)
    {
        var active = _activeChunks[biome];
        var viewers = streaming.ViewerCenters;
        var viewerIndex = viewers.Count == 0
            ? -1
            : Math.Abs(streaming.ViewerCursor % viewers.Count);
        var viewer = viewerIndex < 0 ? default : viewers[viewerIndex];
        var found = false;
        var distance = float.MaxValue;
        chunk = default;

        foreach (var candidate in streaming.PartialLoads.Keys)
        {
            if (!active.Contains(candidate) ||
                biome.LoadedChunks.Contains(candidate) ||
                streaming.PartialUnloads.ContainsKey(candidate))
            {
                continue;
            }

            if (viewerIndex < 0)
            {
                if (found && CompareClassicChunks(candidate, chunk) >= 0)
                    continue;
            }
            else
            {
                var delta = candidate - viewer;
                var candidateDistance = (float) delta.X * delta.X + (float) delta.Y * delta.Y;
                if (found && (candidateDistance > distance ||
                    candidateDistance.Equals(distance) && CompareClassicChunks(candidate, chunk) >= 0))
                {
                    continue;
                }

                distance = candidateDistance;
            }

            chunk = candidate;
            found = true;
        }

        if (found && viewerIndex >= 0)
            streaming.ViewerCursor = (viewerIndex + 1) % viewers.Count;

        return found;
    }

    private bool ClassicStreamingBudgetExpired()
    {
        return Stopwatch.GetElapsedTime(_classicStreamingWorkStarted) >= _classicStreamingWorkBudget;
    }

    private bool ClassicStreamingBudgetExpired(ClassicBiomeStreamingComponent streaming)
    {
        return ClassicStreamingBudgetExpired();
    }

    private bool TryBeginClassicBoundedOperation(bool continuation)
    {
        if (!ClassicStreamingBudgetExpired())
            return true;
        if (!continuation || _classicForcedProgressUsed)
            return false;

        _classicForcedProgressUsed = true;
        return true;
    }

    /// <summary>
    /// Processes every procedural Z biome under one server-tick budget. Rotating the first biome
    /// prevents a busy surface (or any fixed entity-query order) from starving deeper levels.
    /// Viewer chunks remain synchronous because Z movement needs their landing tile this tick.
    /// </summary>
    private void ProcessClassicStreamingBiomes()
    {
        var count = _classicStreamingBiomes.Count;
        if (count == 0)
            return;

        var start = _classicStreamingStartIndex % count;
        for (var i = 0; i < count; i++)
        {
            var entry = _classicStreamingBiomes[(start + i) % count];
            LoadClassicViewerChunks(entry.Biome, entry.GridUid, entry.Grid, entry.Biome.Seed, entry.Streaming);
        }

        for (var pass = 0; pass < 2; pass++)
        {
            var unloadPass = _classicUnloadFirst ? pass == 0 : pass == 1;
            for (var i = 0; i < count; i++)
            {
                var entry = _classicStreamingBiomes[(start + i) % count];
                if (unloadPass)
                {
                    UnloadChunks(entry.Biome, entry.GridUid, entry.Grid, entry.Biome.Seed);
                    continue;
                }

                BuildMarkerChunks(entry.Biome, entry.GridUid, entry.Grid, entry.Biome.Seed);
                LoadClassicPendingMarkerNodes(
                    entry.Biome,
                    entry.GridUid,
                    entry.Grid,
                    entry.Biome.Seed,
                    entry.Streaming);
                LoadClassicBackgroundChunks(entry.Biome, entry.GridUid, entry.Grid, entry.Biome.Seed, entry.Streaming);
            }
        }

        _classicUnloadFirst = !_classicUnloadFirst;
        _classicStreamingStartIndex = (start + 1) % count;
        _classicStreamingBiomes.Clear();
    }

    /// <summary>
    /// Keeps only an immediately reachable adjacent-Z cell ready without subscribing a full
    /// PVS-sized area. Ordinary horizontal movement over a known solid floor must not generate
    /// terrain above and below the player.
    /// </summary>
    private void AddClassicAdjacentLandingChunks(EntityUid viewer, TransformComponent xform, Vector2 worldPos)
    {
        if (xform.MapUid is not { } mapUid ||
            !_classicZMapQuery.TryComp(mapUid, out var zMap) ||
            !_classicZPhysicsQuery.TryComp(viewer, out var zPhysics) ||
            zPhysics.Disabled)
        {
            return;
        }

        var hasBelow = _classicZLevels.TryMapDown((mapUid, zMap), out var below);
        var hasAbove = _classicZLevels.TryMapUp((mapUid, zMap), out var above);
        if (!hasBelow && !hasAbove)
            return;

        var hasCurrentGrid = TryGetClassicViewerGrid(
            xform,
            worldPos,
            out var currentGridUid,
            out var currentGrid,
            out var currentTile);

        if (hasBelow)
        {
            var hasCurrentFloor = hasCurrentGrid &&
                                  ClassicCurrentCellHasFloor(currentGridUid, currentGrid!, currentTile);
            if (!hasCurrentFloor || zPhysics.Velocity < 0f || zPhysics.LocalPosition < 0f)
                AddClassicLandingChunk(below.Owner, worldPos);
        }

        if (hasAbove &&
            (zPhysics.Velocity > 0f ||
             zPhysics.LocalPosition > 0f ||
             hasCurrentGrid && ClassicCurrentCellHasHighGround(currentGridUid, currentGrid!, currentTile)))
        {
            AddClassicLandingChunk(above.Owner, worldPos);
        }
    }

    private bool TryGetClassicViewerGrid(
        TransformComponent xform,
        Vector2 worldPos,
        out EntityUid gridUid,
        [NotNullWhen(true)] out MapGridComponent? grid,
        out Vector2i tile)
    {
        if (xform.GridUid is { } attachedGrid && TryComp(attachedGrid, out grid))
        {
            gridUid = attachedGrid;
            tile = _mapSystem.WorldToTile(gridUid, grid, worldPos);
            return true;
        }

        if (xform.MapUid is { } mapUid && TryComp(mapUid, out grid))
        {
            gridUid = mapUid;
            tile = _mapSystem.WorldToTile(gridUid, grid, worldPos);
            return true;
        }

        if (xform.MapUid is { } fallbackMap &&
            _mapSystem.TryFindGridAt(fallbackMap, worldPos, out gridUid, out grid))
        {
            tile = _mapSystem.WorldToTile(gridUid, grid, worldPos);
            return true;
        }

        gridUid = default;
        grid = null;
        tile = default;
        return false;
    }

    private bool ClassicCurrentCellHasFloor(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (_mapSystem.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
            return true;

        if (!_biomeQuery.TryComp(gridUid, out var biome) || !biome.Enabled)
            return false;

        var chunk = SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize;
        if (biome.ModifiedTiles.TryGetValue(chunk, out var modified) && modified.Contains(tile))
            return false;

        return TryGetBiomeTile(tile, biome.Layers, biome.Seed, (gridUid, grid), out var predicted) &&
               !predicted.Value.IsEmpty;
    }

    private bool ClassicCurrentCellHasHighGround(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (_classicHighGroundQuery.HasComp(uid.Value))
                return true;
        }

        return false;
    }

    private void AddClassicLandingChunk(EntityUid mapUid, Vector2 worldPos)
    {
        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        if (TryAdd(mapUid))
            return;

        if (_mapSystem.TryFindGridAt(mapUid, worldPos, out var containingGridUid, out _) &&
            TryAdd(containingGridUid))
        {
            return;
        }

        foreach (var grid in _mapSystem.GetAllGrids(map.MapId))
        {
            if (TryAdd(grid.Owner))
                return;
        }

        bool TryAdd(EntityUid gridUid)
        {
            if (!_biomeQuery.TryComp(gridUid, out var biome) ||
                !_classicStreamingQuery.TryComp(gridUid, out var streaming) ||
                !TryComp<MapGridComponent>(gridUid, out var grid) ||
                !_activeChunks.TryGetValue(biome, out var active))
            {
                return false;
            }

            var tile = _mapSystem.TileIndicesFor(
                gridUid,
                grid,
                new MapCoordinates(worldPos, map.MapId));
            var chunk = SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize;
            streaming.LandingChunks.Add(chunk);
            streaming.ViewerCells.Add(tile);
            return true;
        }
    }

    private Vector2 ClassicBiomeViewerPosition(EntityUid? attached, EntityUid viewer, TransformComponent xform)
    {
        if (_classicZLevels.IsViewerEye(attached, viewer) &&
            _xformQuery.TryGetComponent(attached, out var ownerXform))
        {
            var ownerPosition = _transform.GetWorldPosition(ownerXform);
            return _eyeQuery.TryComp(viewer, out var zEye) ? ownerPosition + zEye.Offset : ownerPosition;
        }

        var position = _transform.GetWorldPosition(xform);
        return _eyeQuery.TryComp(viewer, out var eye) ? position + eye.Offset : position;
    }

    private float ClassicBiomeViewerScale(EntityUid viewer)
    {
        return _eyeQuery.TryComp(viewer, out var eye) ? MathF.Max(eye.PvsScale, 0.1f) : 1f;
    }

    /// <summary>
    /// Lower Z-eyes are useful only through transparent/open tiles. Wait until each intervening
    /// procedural level has finished its own PVS range, then mirror the client's opening test.
    /// This prevents an opaque surface from generating a complete hidden -Z1 range (and, with a
    /// larger render setting, all deeper ranges) while retaining exact fall landing cells.
    /// </summary>
    private bool ClassicTryGetViewerRange(
        EntityUid? attached,
        EntityUid viewer,
        TransformComponent attachedXform,
        TransformComponent viewerXform,
        Vector2 worldPos,
        float pvsScale,
        out Box2? worldOpeningClip)
    {
        var range = _classicLoadRange * MathF.Max(pvsScale, 0.1f);
        worldOpeningClip = null;
        if (!_classicZLevels.IsViewerEye(attached, viewer) ||
            attachedXform.MapUid is not { } attachedMap ||
            viewerXform.MapUid is not { } viewerMap ||
            !_classicZMapQuery.TryComp(attachedMap, out var attachedZ) ||
            !_classicZMapQuery.TryComp(viewerMap, out var viewerZ) ||
            viewerZ.Depth >= attachedZ.Depth)
        {
            return true;
        }

        var levelsToCheck = attachedZ.Depth - viewerZ.Depth;
        for (var offset = 0; offset < levelsToCheck; offset++)
        {
            EntityUid openingMap;
            if (offset == 0)
            {
                openingMap = attachedMap;
            }
            else if (!_classicZLevels.TryMapOffset((attachedMap, attachedZ), -offset, out var intermediate))
            {
                return false;
            }
            else
            {
                openingMap = intermediate.Owner;
            }

            if (TryGetClassicMapBiome(
                    openingMap,
                    worldPos,
                    out var openingGridUid,
                    out var openingBiome,
                    out var openingGrid))
            {
                var requiredLocalBounds = GetLocalBiomeViewBounds(
                    openingGridUid,
                    openingGrid,
                    worldPos,
                    pvsScale,
                    worldOpeningClip);
                if (requiredLocalBounds.Width <= 0f || requiredLocalBounds.Height <= 0f)
                    return false;

                Box2 openingBounds;
                using (_classicProfiler.Value("ClassicBiome.OpeningSlice"))
                {
                    if (!TryGetClassicOpeningWorldBounds(
                            openingGridUid,
                            openingBiome,
                            openingGrid,
                            requiredLocalBounds,
                            out openingBounds))
                    {
                        return false;
                    }
                }

                var nextClip = openingBounds.Enlarged(3f);
                worldOpeningClip = worldOpeningClip is { } existingClip
                    ? existingClip.Intersect(nextClip)
                    : nextClip;
                if (worldOpeningClip.Value.Width <= 0f || worldOpeningClip.Value.Height <= 0f)
                    return false;
                continue;
            }

            var mapId = Transform(openingMap).MapID;
            var requiredWorldBounds = new Box2(-range, -range, range, range).Translated(worldPos);
            if (worldOpeningClip is { } existingWorldClip)
                requiredWorldBounds = requiredWorldBounds.Intersect(existingWorldClip);
            if (requiredWorldBounds.Width <= 0f || requiredWorldBounds.Height <= 0f)
                return false;

            _classicOpeningBounds.Clear();
            if (!_classicOpeningCache.TryFindOpeningBounds(
                    mapId,
                    requiredWorldBounds,
                    _classicOpeningBounds,
                    out var combinedOpeningBounds,
                    maxOpeningBounds: 4096,
                    exactOpeningBounds: true,
                    _classicOpeningGrids,
                    _mapSystem,
                    _transform,
                    TileDefManager))
            {
                return false;
            }

            var nextWorldClip = combinedOpeningBounds.Enlarged(3f);
            worldOpeningClip = worldOpeningClip is { } previousWorldClip
                ? previousWorldClip.Intersect(nextWorldClip)
                : nextWorldClip;
            if (worldOpeningClip.Value.Width <= 0f || worldOpeningClip.Value.Height <= 0f)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Retains the exact generated chunk range of a Z-eye that is about to be unsubscribed.
    /// Returning before the deadline cancels PendingUnloads through the normal active-chunk path.
    /// </summary>
    public void CacheClassicZLevel(EntityUid eye)
    {
        if (!_xformQuery.TryComp(eye, out var eyeXform) || eyeXform.MapUid is not { } mapUid)
            return;

        var worldPos = ClassicBiomeViewerPosition(null, eye, eyeXform);
        if (!TryGetClassicMapBiome(
                mapUid,
                worldPos,
                out var gridUid,
                out var biome,
                out var grid) ||
            !_classicStreamingQuery.TryComp(gridUid, out var streaming))
        {
            return;
        }

        var duration = streaming.ZLevelCacheDuration < TimeSpan.Zero
            ? TimeSpan.Zero
            : streaming.ZLevelCacheDuration;
        var deadline = _classicTiming.CurTime + duration;
        var bounds = GetLocalBiomeViewBounds(
            gridUid,
            grid,
            worldPos,
            ClassicBiomeViewerScale(eye));
        var chunks = new ChunkIndicesEnumerator(bounds, ChunkSize);
        while (chunks.MoveNext(out var chunkIndices))
        {
            var chunk = chunkIndices.Value * ChunkSize;
            if (!biome.LoadedChunks.Contains(chunk) &&
                !streaming.PartialLoads.ContainsKey(chunk) &&
                !streaming.PartialUnloads.ContainsKey(chunk))
            {
                continue;
            }

            streaming.CachedZChunks[chunk] = deadline;
            if (streaming.PendingUnloads.TryGetValue(chunk, out var pending) && pending < deadline)
                streaming.PendingUnloads[chunk] = deadline;
            if (streaming.PartialUnloads.TryGetValue(chunk, out var partial))
                partial.Phase = ClassicBiomePartialUnloadPhase.Restoring;
        }

        TrimClassicZLevelCache(streaming);
    }

    private TimeSpan GetClassicUnloadDeadline(ClassicBiomeStreamingComponent streaming, Vector2i chunk)
    {
        var delay = streaming.UnloadDelay < TimeSpan.Zero ? TimeSpan.Zero : streaming.UnloadDelay;
        var deadline = _classicTiming.CurTime + delay;
        return streaming.CachedZChunks.TryGetValue(chunk, out var cached) && cached > deadline
            ? cached
            : deadline;
    }

    private void PruneClassicZLevelCache(ClassicBiomeStreamingComponent streaming)
    {
        _classicStaleUnloadChunks.Clear();
        foreach (var (chunk, deadline) in streaming.CachedZChunks)
        {
            if (deadline <= _classicTiming.CurTime)
                _classicStaleUnloadChunks.Add(chunk);
        }

        foreach (var chunk in _classicStaleUnloadChunks)
            streaming.CachedZChunks.Remove(chunk);
        TrimClassicZLevelCache(streaming);
    }

    private void TrimClassicZLevelCache(ClassicBiomeStreamingComponent streaming)
    {
        var maxChunks = Math.Clamp(streaming.MaxZLevelCachedChunks, 0, 4096);
        while (streaming.CachedZChunks.Count > maxChunks)
        {
            var found = false;
            var oldestChunk = default(Vector2i);
            var oldestDeadline = TimeSpan.MaxValue;
            foreach (var (chunk, deadline) in streaming.CachedZChunks)
            {
                if (found && deadline >= oldestDeadline)
                    continue;

                found = true;
                oldestChunk = chunk;
                oldestDeadline = deadline;
            }

            if (!found)
                break;

            streaming.CachedZChunks.Remove(oldestChunk);
            if (!streaming.PendingUnloads.TryGetValue(oldestChunk, out var pending))
                continue;

            var delay = streaming.UnloadDelay < TimeSpan.Zero ? TimeSpan.Zero : streaming.UnloadDelay;
            var ordinaryDeadline = _classicTiming.CurTime + delay;
            if (pending > ordinaryDeadline)
                streaming.PendingUnloads[oldestChunk] = ordinaryDeadline;
        }
    }

    private bool TryGetClassicOpeningWorldBounds(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        Box2 localBounds,
        out Box2 openingWorldBounds)
    {
        if (!_classicStreamingQuery.TryComp(gridUid, out var streaming))
            return TryGetClassicOpeningWorldBoundsAtomic(gridUid, biome, grid, localBounds, out openingWorldBounds);

        var requiredFirstChunk = (localBounds.BottomLeft / ChunkSize).Floored();
        var requiredLastChunk = (localBounds.TopRight / ChunkSize).Floored();
        var firstTile = new Vector2i(
            (int) MathF.Floor(localBounds.Left) - 1,
            (int) MathF.Floor(localBounds.Bottom) - 1);
        var lastTile = new Vector2i(
            (int) MathF.Floor(localBounds.Right) + 1,
            (int) MathF.Floor(localBounds.Top) + 1);
        var key = new ClassicBiomeOpeningScanKey(
            requiredFirstChunk,
            requiredLastChunk,
            firstTile,
            lastTile);
        var revision = _classicOpeningCache.GetRevision((gridUid, grid));
        var created = false;

        if (!streaming.OpeningScans.TryGetValue(key, out var scan) || scan.Revision != revision)
        {
            scan = new ClassicBiomeOpeningScanState(
                requiredFirstChunk,
                requiredLastChunk,
                firstTile,
                lastTile,
                revision,
                streaming.OpeningScanGeneration,
                _classicTiming.CurTime);
            streaming.OpeningScans[key] = scan;
            created = true;
        }

        scan.LastRequestGeneration = streaming.OpeningScanGeneration;
        scan.LastRequestTime = _classicTiming.CurTime;
        if (scan.Phase == ClassicBiomeOpeningScanPhase.Complete)
            return TryGetClassicOpeningScanBounds(gridUid, grid, scan, out openingWorldBounds);

        var continuation = !created &&
                           (scan.Phase != ClassicBiomeOpeningScanPhase.WaitingForChunks ||
                            scan.NextChunk != scan.RequiredFirstChunk);
        if (!TryBeginClassicBoundedOperation(continuation))
        {
            openingWorldBounds = default;
            return false;
        }

        var maxCells = Math.Clamp(streaming.OpeningCellsPerSlice, 1, ClassicChunkCells);
        var processed = 0;

        while (scan.Phase == ClassicBiomeOpeningScanPhase.WaitingForChunks &&
               scan.NextChunk.X <= scan.RequiredLastChunk.X)
        {
            var chunkOrigin = scan.NextChunk * ChunkSize;
            if (!biome.LoadedChunks.Contains(chunkOrigin))
            {
                openingWorldBounds = default;
                return false;
            }

            var hasNext = AdvanceClassicOpeningCursor(
                ref scan.NextChunk,
                scan.RequiredFirstChunk,
                scan.RequiredLastChunk);
            processed++;
            if (!hasNext)
                scan.Phase = ClassicBiomeOpeningScanPhase.Scanning;

            if (processed >= maxCells || ClassicStreamingBudgetExpired())
            {
                openingWorldBounds = default;
                return false;
            }
        }

        while (scan.Phase == ClassicBiomeOpeningScanPhase.Scanning &&
               scan.NextTile.X <= scan.LastTile.X)
        {
            var tile = scan.NextTile;
            var guardedChunk = SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize;
            if (biome.LoadedChunks.Contains(guardedChunk) &&
                ClassicZLevelOpeningCache.IsOpeningTile((gridUid, grid), tile, _mapSystem, TileDefManager))
            {
                scan.HasOpening = true;
                scan.FirstOpeningTile = Vector2i.ComponentMin(scan.FirstOpeningTile, tile);
                scan.LastOpeningTile = Vector2i.ComponentMax(scan.LastOpeningTile, tile);
            }

            var hasNext = AdvanceClassicOpeningCursor(ref scan.NextTile, scan.FirstTile, scan.LastTile);
            processed++;
            if (!hasNext)
                break;

            if (processed >= maxCells || ClassicStreamingBudgetExpired())
            {
                openingWorldBounds = default;
                return false;
            }
        }

        if (_classicOpeningCache.GetRevision((gridUid, grid)) != scan.Revision)
        {
            streaming.OpeningScans.Remove(key);
            openingWorldBounds = default;
            return false;
        }

        scan.Phase = ClassicBiomeOpeningScanPhase.Complete;
        return TryGetClassicOpeningScanBounds(gridUid, grid, scan, out openingWorldBounds);
    }

    private bool TryGetClassicOpeningScanBounds(
        EntityUid gridUid,
        MapGridComponent grid,
        ClassicBiomeOpeningScanState scan,
        out Box2 openingWorldBounds)
    {
        if (!scan.HasOpening)
        {
            openingWorldBounds = default;
            return false;
        }

        var tileSize = MathF.Max(grid.TileSize, float.Epsilon);
        var localMetres = new Box2(
            (Vector2) scan.FirstOpeningTile * tileSize,
            (Vector2) (scan.LastOpeningTile + Vector2i.One) * tileSize);
        openingWorldBounds = _transform.GetWorldMatrix(gridUid).TransformBox(localMetres);
        return true;
    }

    private static bool AdvanceClassicOpeningCursor(
        ref Vector2i cursor,
        Vector2i first,
        Vector2i last)
    {
        if (cursor.Y < last.Y)
        {
            cursor = new Vector2i(cursor.X, cursor.Y + 1);
            return true;
        }

        cursor = new Vector2i(cursor.X + 1, first.Y);
        return cursor.X <= last.X;
    }

    private bool TryGetClassicOpeningWorldBoundsAtomic(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        Box2 localBounds,
        out Box2 openingWorldBounds)
    {
        var requiredChunks = new ChunkIndicesEnumerator(localBounds, ChunkSize);
        while (requiredChunks.MoveNext(out var chunkOrigin))
        {
            if (!biome.LoadedChunks.Contains(chunkOrigin.Value * ChunkSize))
            {
                openingWorldBounds = default;
                return false;
            }
        }

        var firstTile = new Vector2i(
            (int) MathF.Floor(localBounds.Left) - 1,
            (int) MathF.Floor(localBounds.Bottom) - 1);
        var lastTile = new Vector2i(
            (int) MathF.Floor(localBounds.Right) + 1,
            (int) MathF.Floor(localBounds.Top) + 1);
        var firstChunk = SharedMapSystem.GetChunkIndices(firstTile, ChunkSize);
        var lastChunk = SharedMapSystem.GetChunkIndices(lastTile, ChunkSize);
        var found = false;
        var combinedLocalBounds = default(Box2);

        for (var chunkX = firstChunk.X; chunkX <= lastChunk.X; chunkX++)
        for (var chunkY = firstChunk.Y; chunkY <= lastChunk.Y; chunkY++)
        {
            var chunk = new Vector2i(chunkX, chunkY);
            var chunkOrigin = chunk * ChunkSize;
            if (!biome.LoadedChunks.Contains(chunkOrigin))
                continue;

            var chunkLast = chunkOrigin + new Vector2i(ChunkSize - 1, ChunkSize - 1);
            var searchStart = Vector2i.ComponentMax(firstTile, chunkOrigin);
            var searchEnd = Vector2i.ComponentMin(lastTile, chunkLast);
            if (!_classicOpeningCache.TryGetOpeningBounds(
                    (gridUid, grid),
                    searchStart,
                    searchEnd,
                    _mapSystem,
                    TileDefManager,
                    out var localOpeningBounds))
            {
                continue;
            }

            combinedLocalBounds = found
                ? combinedLocalBounds.Union(localOpeningBounds)
                : localOpeningBounds;
            found = true;
        }

        if (!found)
        {
            openingWorldBounds = default;
            return false;
        }

        openingWorldBounds = _transform.GetWorldMatrix(gridUid).TransformBox(combinedLocalBounds);
        return true;
    }

    private bool TryGetClassicMapBiome(
        EntityUid mapUid,
        Vector2 worldPos,
        out EntityUid gridUid,
        [NotNullWhen(true)] out BiomeComponent? biome,
        [NotNullWhen(true)] out MapGridComponent? grid)
    {
        if (_biomeQuery.TryComp(mapUid, out biome) &&
            TryComp<MapGridComponent>(mapUid, out grid))
        {
            gridUid = mapUid;
            return true;
        }

        if (_mapSystem.TryFindGridAt(mapUid, worldPos, out var containingGridUid, out var containingGrid) &&
            _biomeQuery.TryComp(containingGridUid, out biome))
        {
            gridUid = containingGridUid;
            grid = containingGrid;
            return true;
        }

        if (TryComp<MapComponent>(mapUid, out var map))
        {
            foreach (var candidateGrid in _mapSystem.GetAllGrids(map.MapId))
            {
                if (_biomeQuery.TryComp(candidateGrid.Owner, out biome))
                {
                    gridUid = candidateGrid.Owner;
                    grid = candidateGrid.Comp;
                    return true;
                }
            }
        }

        gridUid = default;
        biome = null;
        grid = null;
        return false;
    }

    private void OnClassicTileChanged(ref TileChangedEvent args)
    {
        _classicOpeningCache.InvalidateTiles(args.Entity, args.Changes);

        if (!_biomeQuery.TryComp(args.Entity.Owner, out var biome))
            return;

        if (_classicBiomeTileWrites.Contains(args.Entity.Owner))
            return;

        foreach (var change in args.Changes)
        {
            var chunkOrigin = SharedMapSystem.GetChunkIndices(change.GridIndices, ChunkSize) * ChunkSize;
            biome.ModifiedTiles.GetOrNew(chunkOrigin).Add(change.GridIndices);
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

    private int ClassicBiomeDepth(EntityUid gridUid)
    {
        return _classicZMapQuery.TryComp(Transform(gridUid).MapUid, out var zMap) ? zMap.Depth : 0;
    }

    private bool ClassicCanUnloadBiomeEntity(EntityUid gridUid, EntityUid uid, int depth)
    {
        if (_classicAlwaysUnloadQuery.HasComponent(uid))
            return true;

        if (!_classicStreamingQuery.HasComp(gridUid))
            return EntityManager.IsDefault(uid);

        if (_classicDamageableQuery.TryComp(uid, out var damageable) && damageable.TotalDamage != 0)
            return false;

        var metadata = MetaData(uid);
        if (metadata.EntityPrototype is not { } prototype ||
            metadata.EntityName != prototype.Name || metadata.EntityDescription != prototype.Description)
            return false;

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

            if (component is TagComponent tags && entry.Component is TagComponent prototypeTags)
            {
                if (tags.Tags.Count != prototypeTags.Tags.Count || !_tags.HasAllTags(tags, prototypeTags.Tags))
                    return false;
                continue;
            }

            if (component is Content.Server.Gatherable.Components.GatherableComponent gatherable &&
                entry.Component is Content.Server.Gatherable.Components.GatherableComponent prototypeGatherable)
            {
                if (gatherable.GatherOffset != prototypeGatherable.GatherOffset ||
                    !_classicSerialization.DataFieldEquals(gatherable.ToolWhitelist, prototypeGatherable.ToolWhitelist) ||
                    !_classicSerialization.DataFieldEquals(gatherable.Loot, prototypeGatherable.Loot) ||
                    !_classicSerialization.DataFieldEquals(gatherable.ToolQualities, prototypeGatherable.ToolQualities))
                {
                    return false;
                }

                continue;
            }

            var expected = entry.Component;
            if (component is ClassicZPhysicsComponent && expected is ClassicZPhysicsComponent prototypePhysics)
            {
                var key = (prototype, depth);
                if (!_classicZPhysicsDefaults.TryGetValue(key, out var physics))
                {
                    physics = _classicSerialization.CreateCopy(prototypePhysics, notNullableOverride: true);
                    physics.CurrentZLevel = depth;
                    _classicZPhysicsDefaults[key] = physics;
                }
                expected = physics;
            }

            if (!_classicSerialization.DataFieldEquals(type, component, expected))
                return false;
        }

        return remaining == 0;
    }

    private bool ClassicCanUnloadBiomeEntity(EntityUid gridUid, EntityUid uid)
    {
        return ClassicCanUnloadBiomeEntity(gridUid, uid, ClassicBiomeDepth(gridUid));
    }

    private bool ClassicHasPersistentBiomeDependent(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        EntityUid biomeEntity)
    {
        if (!_classicGeyserOutletQuery.HasComp(biomeEntity))
            return false;

        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (other.Value != biomeEntity && _classicGeyserGeneratorQuery.HasComp(other.Value))
                return true;
        }

        return false;
    }

    private Dictionary<Vector2i, EntityUid> GetClassicReplaceableMarkerEntities(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Box2i bounds)
    {
        var result = new Dictionary<Vector2i, EntityUid>();
        var depth = ClassicBiomeDepth(gridUid);
        foreach (var loaded in biome.LoadedEntities.Values)
        {
            foreach (var (entity, tile) in loaded)
            {
                if (!bounds.Contains(tile) || Deleted(entity) ||
                    !_xformQuery.TryGetComponent(entity, out var xform) || !xform.Anchored ||
                    _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates) != tile ||
                    ClassicHasPersistentBiomeDependent(gridUid, grid, tile, entity) ||
                    !ClassicCanUnloadBiomeEntity(gridUid, entity, depth))
                {
                    continue;
                }

                result.TryAdd(tile, entity);
            }
        }

        return result;
    }

    private bool TryRemoveClassicMarkerBaseline(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        Vector2i node)
    {
        if (!biome.LoadedEntities.TryGetValue(chunk, out var loaded))
            return true;

        EntityUid baseline = default;
        foreach (var (entity, tile) in loaded)
        {
            if (tile != node)
                continue;
            baseline = entity;
            break;
        }

        if (baseline == EntityUid.Invalid)
            return true;

        if (!Deleted(baseline))
        {
            if (!_xformQuery.TryGetComponent(baseline, out var xform) || !xform.Anchored ||
                _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates) != node ||
                ClassicHasPersistentBiomeDependent(gridUid, grid, node, baseline) ||
                !ClassicCanUnloadBiomeEntity(gridUid, baseline, ClassicBiomeDepth(gridUid)))
            {
                return false;
            }

            Del(baseline);
        }

        loaded.Remove(baseline);
        if (loaded.Count == 0)
            biome.LoadedEntities.Remove(chunk);
        return true;
    }

    private bool TryGetActiveBiome(
        TransformComponent xform,
        Vector2 worldPos,
        out EntityUid gridUid,
        [NotNullWhen(true)] out BiomeComponent? biome,
        [NotNullWhen(true)] out MapGridComponent? grid)
    {
        if (xform.MapUid is { } mapUid &&
            _biomeQuery.TryGetComponent(mapUid, out biome) &&
            TryComp<MapGridComponent>(mapUid, out grid))
        {
            gridUid = mapUid;
            return true;
        }

        if (xform.GridUid is { } parentGridUid &&
            _biomeQuery.TryGetComponent(parentGridUid, out biome) &&
            TryComp<MapGridComponent>(parentGridUid, out grid))
        {
            gridUid = parentGridUid;
            return true;
        }

        if (xform.MapUid is { } fallbackMap &&
            TryGetClassicMapBiome(fallbackMap, worldPos, out gridUid, out biome, out grid))
        {
            return true;
        }

        gridUid = default;
        biome = null;
        grid = null;
        return false;
    }

    /// <summary>
    /// Compatibility overloads keep the upstream update fallback buildable. The Classic hook
    /// handles the frame first, but retaining the fallback makes future upstream merges local.
    /// </summary>
    private bool TryGetActiveBiome(
        TransformComponent xform,
        [NotNullWhen(true)] out BiomeComponent? biome)
    {
        return TryGetActiveBiome(
            xform,
            _transform.GetWorldPosition(xform),
            out _,
            out biome,
            out _);
    }

    private bool TryUpdateClassicBiomeStreaming(float frameTime)
    {
        var biomes = AllEntityQuery<BiomeComponent>();
        var hasClassicBudget = false;
        var classicBudget = TimeSpan.Zero;

        while (biomes.MoveNext(out var biome))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            _activeChunks.Add(biome, _tilePool.Get());
            if (_classicStreamingQuery.TryComp(biome.Owner, out var streaming))
            {
                streaming.ViewerChunks.Clear();
                streaming.ViewerCells.Clear();
                streaming.LandingChunks.Clear();
                streaming.ViewerCenters.Clear();
                unchecked
                {
                    streaming.OpeningScanGeneration++;
                    if (streaming.OpeningScanGeneration == 0)
                        streaming.OpeningScanGeneration++;
                }

                if (biome.Enabled && (!hasClassicBudget || streaming.WorkBudget < classicBudget))
                {
                    classicBudget = streaming.WorkBudget;
                    hasClassicBudget = true;
                }
            }
            if (biome.MarkerLayers.Count > 0 || biome.ForcedMarkerLayers.Count > 0)
                _markerChunks.GetOrNew(biome);
        }

        BeginClassicStreamingBudget(hasClassicBudget ? classicBudget : TimeSpan.Zero);

        foreach (var pSession in Filter.GetAllPlayers(_playerManager))
        {
            if (pSession.Status != SessionStatus.InGame)
                continue;

            if (!_xformQuery.TryGetComponent(pSession.AttachedEntity, out var attachedXform) ||
                !CanLoad(pSession.AttachedEntity.Value))
            {
                continue;
            }

            var attachedBodyPos = _transform.GetWorldPosition(attachedXform);
            var attachedWorldPos = ClassicBiomeViewerPosition(
                pSession.AttachedEntity,
                pSession.AttachedEntity.Value,
                attachedXform);
            if (_handledEntities.Add(pSession.AttachedEntity.Value) &&
                TryGetActiveBiome(
                    attachedXform,
                    attachedBodyPos,
                    out var attachedGridUid,
                    out var attachedBiome,
                    out var attachedGrid) &&
                attachedBiome.Enabled)
            {
                AddChunksInRange(
                    attachedGridUid,
                    attachedGrid,
                    attachedBiome,
                    attachedWorldPos,
                    ClassicBiomeViewerScale(pSession.AttachedEntity.Value),
                    physicalViewer: pSession.AttachedEntity.Value,
                    physicalViewerXform: attachedXform,
                    physicalWorldPos: attachedBodyPos);

                foreach (var layer in attachedBiome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInRange(
                        attachedGridUid,
                        attachedGrid,
                        attachedBiome,
                        attachedWorldPos,
                        layerProto);
                }
            }

            AddClassicAdjacentLandingChunks(pSession.AttachedEntity.Value, attachedXform, attachedBodyPos);

            foreach (var viewer in pSession.ViewSubscriptions)
            {
                if (!_handledEntities.Add(viewer) ||
                    !_xformQuery.TryGetComponent(viewer, out var viewerXform) ||
                    !CanLoad(viewer))
                {
                    continue;
                }

                var worldPos = ClassicBiomeViewerPosition(pSession.AttachedEntity, viewer, viewerXform);
                if (!TryGetActiveBiome(
                        viewerXform,
                        worldPos,
                        out var viewerGridUid,
                        out var viewerBiome,
                        out var viewerGrid) ||
                    !viewerBiome.Enabled)
                {
                    continue;
                }

                var pvsScale = ClassicBiomeViewerScale(viewer);
                if (!ClassicTryGetViewerRange(
                        pSession.AttachedEntity,
                        viewer,
                        attachedXform,
                        viewerXform,
                        worldPos,
                        pvsScale,
                        out _))
                {
                    continue;
                }

                var activeLocalBounds = AddChunksInRange(
                    viewerGridUid,
                    viewerGrid,
                    viewerBiome,
                    worldPos,
                    pvsScale);

                foreach (var layer in viewerBiome.MarkerLayers)
                {
                    var layerProto = ProtoManager.Index(layer);
                    AddMarkerChunksInBounds(viewerBiome, activeLocalBounds, layerProto);
                }
            }
        }

        var loadBiomes = AllEntityQuery<BiomeComponent, MapGridComponent>();
        _classicStreamingBiomes.Clear();

        while (loadBiomes.MoveNext(out var gridUid, out var biome, out var grid))
        {
            if (biome.LifeStage < ComponentLifeStage.Running)
                continue;

            if (!biome.Enabled)
                continue;

            if (_classicStreamingQuery.TryComp(gridUid, out var streaming))
            {
                PruneClassicOpeningScans(streaming);
                _classicStreamingBiomes.Add((gridUid, biome, grid, streaming));
                continue;
            }

            LoadChunks(biome, gridUid, grid, biome.Seed);
            UnloadChunks(biome, gridUid, grid, biome.Seed);
        }

        ProcessClassicStreamingBiomes();

        _handledEntities.Clear();

        foreach (var tiles in _activeChunks.Values)
        {
            _tilePool.Return(tiles);
        }

        _activeChunks.Clear();
        _markerChunks.Clear();
        return true;
    }


    private Box2 AddChunksInRange(
        EntityUid gridUid,
        MapGridComponent grid,
        BiomeComponent biome,
        Vector2 worldPos,
        float pvsScale = 1f,
        EntityUid? physicalViewer = null,
        TransformComponent? physicalViewerXform = null,
        Vector2? physicalWorldPos = null,
        Box2? worldClip = null)
    {
        ClassicBiomeStreamingComponent? streaming = null;
        var loadArea = GetLocalBiomeViewBounds(gridUid, grid, worldPos, pvsScale, worldClip);

        if (_classicStreamingQuery.TryComp(biome.Owner, out streaming))
        {
            var priorityTile = loadArea.Center.Floored();
            var viewerChunk = SharedMapSystem.GetChunkIndices(priorityTile, ChunkSize) * ChunkSize;
            if (!streaming.ViewerCenters.Contains(viewerChunk))
                streaming.ViewerCenters.Add(viewerChunk);
            if (physicalViewer is { } viewerUid && physicalViewerXform != null)
            {
                var tile = _mapSystem.WorldToTile(gridUid, grid, physicalWorldPos ?? worldPos);
                var physicalChunk = SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize;
                streaming.ViewerChunks.Add(physicalChunk);
                AddClassicViewerSafetyCells(
                    viewerUid,
                    physicalViewerXform,
                    gridUid,
                    grid,
                    streaming,
                    tile);
            }
        }

        var enumerator = new ChunkIndicesEnumerator(loadArea, ChunkSize);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var chunk = chunkOrigin.Value * ChunkSize;
            _activeChunks[biome].Add(chunk);
        }

        return loadArea;
    }

    /// <summary>
    /// Materializes the physical cells a body can occupy before the next physics solve. A fixed
    /// 3x3 square made a stationary player synchronously initialize nine wall entities; the hard
    /// fixture footprint plus a short relative-velocity sweep normally needs only one or two.
    /// </summary>
    private void AddClassicViewerSafetyCells(
        EntityUid viewerUid,
        TransformComponent viewerXform,
        EntityUid gridUid,
        MapGridComponent grid,
        ClassicBiomeStreamingComponent streaming,
        Vector2i tile)
    {
        var safetyRadius = Math.Clamp(streaming.ViewerSafetyRadius, 0, 2);
        if (!_physicsQuery.TryComp(viewerUid, out var body) ||
            !_fixturesQuery.TryComp(viewerUid, out var fixtures))
        {
            AddFallback();
            return;
        }

        var relativeTransform = _physics.GetRelativePhysicsTransform(
            (viewerUid, viewerXform),
            (gridUid, (TransformComponent?) null));
        var foundHardFixture = false;
        var bounds = Box2.Empty;
        var fixtureRadius = 0f;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            for (var child = 0; child < fixture.Shape.ChildCount; child++)
            {
                var childBounds = fixture.Shape.ComputeAABB(relativeTransform, child);
                if (!childBounds.IsValid() || childBounds.HasNan())
                {
                    AddFallback();
                    return;
                }

                bounds = foundHardFixture ? bounds.Union(childBounds) : childBounds;
                foundHardFixture = true;
                fixtureRadius = MathF.Max(fixtureRadius, Vector2.Distance(relativeTransform.Position, childBounds.BottomLeft));
                fixtureRadius = MathF.Max(fixtureRadius, Vector2.Distance(relativeTransform.Position, childBounds.BottomRight));
                fixtureRadius = MathF.Max(fixtureRadius, Vector2.Distance(relativeTransform.Position, childBounds.TopLeft));
                fixtureRadius = MathF.Max(fixtureRadius, Vector2.Distance(relativeTransform.Position, childBounds.TopRight));
            }
        }

        if (!foundHardFixture)
        {
            AddFallback();
            return;
        }

        var predictionSeconds = (float) _classicTiming.TickPeriod.TotalSeconds *
                                Math.Clamp(streaming.ViewerPredictionTicks, 0, 4);
        var viewerVelocity = _physics.GetMapLinearVelocity(viewerUid, body, viewerXform);
        var gridVelocity = _physics.GetMapLinearVelocity(
            new EntityCoordinates(gridUid, relativeTransform.Position));
        var localVelocity = Vector2.TransformNormal(
            viewerVelocity - gridVelocity,
            _transform.GetInvWorldMatrix(gridUid));
        bounds = bounds.Union(bounds.Translated(localVelocity * predictionSeconds));

        var relativeAngularVelocity = _physics.GetMapAngularVelocity(viewerUid, body, viewerXform) -
                                      _physics.GetMapAngularVelocity(gridUid);
        var rotationMargin = fixtureRadius * MathF.Abs(relativeAngularVelocity) * predictionSeconds;
        var numericAndAccelerationMargin = Math.Clamp(streaming.ViewerSafetyMargin, 0f, grid.TileSize);
        bounds = bounds.Enlarged(rotationMargin + numericAndAccelerationMargin);

        const float tileEdgeEpsilon = 0.0001f;
        var firstX = (int) MathF.Floor((bounds.Left - tileEdgeEpsilon) / grid.TileSize);
        var firstY = (int) MathF.Floor((bounds.Bottom - tileEdgeEpsilon) / grid.TileSize);
        var lastX = (int) MathF.Ceiling((bounds.Right + tileEdgeEpsilon) / grid.TileSize) - 1;
        var lastY = (int) MathF.Ceiling((bounds.Top + tileEdgeEpsilon) / grid.TileSize) - 1;

        firstX = Math.Clamp(firstX, tile.X - safetyRadius, tile.X + safetyRadius);
        firstY = Math.Clamp(firstY, tile.Y - safetyRadius, tile.Y + safetyRadius);
        lastX = Math.Clamp(lastX, tile.X - safetyRadius, tile.X + safetyRadius);
        lastY = Math.Clamp(lastY, tile.Y - safetyRadius, tile.Y + safetyRadius);
        streaming.ViewerCells.Add(tile);
        for (var x = firstX; x <= lastX; x++)
        for (var y = firstY; y <= lastY; y++)
            streaming.ViewerCells.Add(new Vector2i(x, y));
        return;

        void AddFallback()
        {
            for (var x = -safetyRadius; x <= safetyRadius; x++)
            for (var y = -safetyRadius; y <= safetyRadius; y++)
                streaming.ViewerCells.Add(tile + new Vector2i(x, y));
        }
    }

    /// <summary>
    /// Matches PVS' grid handling: transform only the eye center into grid-local coordinates and
    /// build an axis-aligned square there. Transforming a world AABB would over-expand rotated
    /// grids and synchronously request chunks PVS itself cannot see.
    /// </summary>
    private Box2 GetLocalBiomeViewBounds(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldPos,
        float pvsScale,
        Box2? worldClip = null)
    {
        var tileSize = MathF.Max(grid.TileSize, float.Epsilon);
        var inverse = _transform.GetInvWorldMatrix(gridUid);
        var localCenter = Vector2.Transform(worldPos, inverse) / tileSize;
        var range = _classicLoadRange * MathF.Max(pvsScale, 0.1f) / tileSize;
        var bounds = new Box2(-range, -range, range, range).Translated(localCenter);
        if (worldClip is not { } clip)
            return bounds;

        var localClipMetres = inverse.TransformBox(clip);
        var localClip = new Box2(
            localClipMetres.BottomLeft / tileSize,
            localClipMetres.TopRight / tileSize);
        return bounds.Intersect(localClip);
    }

    private void AddMarkerChunksInRange(
        EntityUid gridUid,
        MapGridComponent grid,
        BiomeComponent biome,
        Vector2 worldPos,
        IBiomeMarkerLayer layer)
    {
        var loadArea = new Box2(0, 0, layer.Size, layer.Size);
        var halfLayer = new Vector2(layer.Size / 2f);
        var tileSize = MathF.Max(grid.TileSize, float.Epsilon);
        var localPos = Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(gridUid)) / tileSize;

        var enumerator = new ChunkIndicesEnumerator(loadArea.Translated(localPos - halfLayer), layer.Size);

        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var lay = _markerChunks[biome].GetOrNew(layer.ID);
            lay.Add(chunkOrigin.Value * layer.Size);
        }
    }

    private void AddMarkerChunksInBounds(BiomeComponent biome, Box2 localBounds, IBiomeMarkerLayer layer)
    {
        var enumerator = new ChunkIndicesEnumerator(localBounds, layer.Size);
        while (enumerator.MoveNext(out var chunkOrigin))
        {
            var chunks = _markerChunks[biome].GetOrNew(layer.ID);
            chunks.Add(chunkOrigin.Value * layer.Size);
        }
    }

    private static bool ClassicMarkerRequestsComplete(
        Dictionary<string, HashSet<Vector2i>> requested,
        Dictionary<string, HashSet<Vector2i>> loaded)
    {
        foreach (var (layer, chunks) in requested)
        {
            if (!loaded.TryGetValue(layer, out var completed) || chunks.Any(chunk => !completed.Contains(chunk)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Advances one deterministic Classic marker-area snapshot. The generic biome path above
    /// intentionally retains its original parallel, atomic <see cref="GetMarkerNodes"/> call.
    /// </summary>
    private void BuildClassicMarkerChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        Dictionary<string, HashSet<Vector2i>> markers,
        ClassicBiomeStreamingComponent streaming)
    {
        var scan = streaming.MarkerAreaScan;
        if (scan != null && !ClassicMarkerRequestExists(component, markers, scan))
        {
            DisposeClassicMarkerScan(scan);
            streaming.MarkerAreaScan = null;
            scan = null;
        }

        var continuation = scan != null;
        if (!TryBeginClassicBoundedOperation(continuation))
            return;

        if (scan == null)
        {
            var layerIndex = 0;
            foreach (var (layer, chunks) in markers)
            {
                layerIndex++;
                var found = false;
                var markerChunk = default(Vector2i);
                foreach (var candidate in chunks)
                {
                    if (component.LoadedMarkers.TryGetValue(layer, out var completed) && completed.Contains(candidate) ||
                        found && CompareClassicMarkerChunks(candidate, markerChunk) >= 0)
                    {
                        continue;
                    }

                    markerChunk = candidate;
                    found = true;
                }

                if (!found)
                    continue;

                var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
                var buffer = (int) (layerProto.Radius / 2f);
                var bounds = new Box2i(markerChunk + buffer, markerChunk + layerProto.Size - buffer);
                var count = (int) (bounds.Area / (layerProto.Radius * layerProto.Radius));
                count = Math.Min(count, layerProto.MaxCount);
                DebugTools.Assert(count > 0);

                scan = new ClassicBiomeMarkerAreaScanState(
                    layer,
                    markerChunk,
                    component.ForcedMarkerLayers.Contains(layer),
                    bounds,
                    count,
                    seed + markerChunk.X * ChunkSize + markerChunk.Y + layerIndex);
                streaming.MarkerAreaScan = scan;
                break;
            }
        }

        if (scan == null)
            return;

        var layerPrototype = ProtoManager.Index<BiomeMarkerLayerPrototype>(scan.Layer);
        var cellsPerSlice = Math.Clamp(streaming.MarkerAreaCellsPerSlice, 1, ClassicChunkCells);
        var totalCells = scan.Bounds.Width * scan.Bounds.Height;
        var processed = 0;
        var depth = ClassicBiomeDepth(gridUid);

        while (scan.Phase == ClassicBiomeMarkerAreaScanPhase.Scanning &&
               scan.NextCell < totalCells &&
               processed < cellsPerSlice)
        {
            var x = scan.NextCell / scan.Bounds.Height;
            var y = scan.NextCell % scan.Bounds.Height;
            var node = new Vector2i(scan.Bounds.Left + x, scan.Bounds.Bottom + y);
            ScanClassicMarkerNode(component, gridUid, grid, layerPrototype, scan, node, depth);
            scan.NextCell++;
            processed++;

            if (ClassicStreamingBudgetExpired())
                return;
        }

        if (scan.Phase == ClassicBiomeMarkerAreaScanPhase.Scanning)
        {
            if (scan.NextCell < totalCells)
                return;

            scan.Phase = ClassicBiomeMarkerAreaScanPhase.Selecting;
            if (processed >= cellsPerSlice || ClassicStreamingBudgetExpired())
                return;
        }

        if (!AdvanceClassicMarkerSelection(scan, layerPrototype, cellsPerSlice, ref processed))
            return;

        PublishClassicMarkerAreaScan(component, scan);
        DisposeClassicMarkerScan(scan);
        streaming.MarkerAreaScan = null;
    }

    private static void DisposeClassicMarkerScan(ClassicBiomeMarkerAreaScanState? scan)
    {
        if (scan is not { SeedPickActive: true })
            return;

        scan.SeedEnumerator.Dispose();
        scan.SeedPickActive = false;
    }

    private static bool ClassicMarkerRequestExists(
        BiomeComponent component,
        Dictionary<string, HashSet<Vector2i>> markers,
        ClassicBiomeMarkerAreaScanState scan)
    {
        return markers.TryGetValue(scan.Layer, out var requested) &&
               requested.Contains(scan.MarkerChunk) &&
               (!component.LoadedMarkers.TryGetValue(scan.Layer, out var completed) ||
                !completed.Contains(scan.MarkerChunk)) &&
               component.ForcedMarkerLayers.Contains(scan.Layer) == scan.Forced;
    }

    private static int CompareClassicMarkerChunks(Vector2i a, Vector2i b)
    {
        var result = a.X.CompareTo(b.X);
        return result != 0 ? result : a.Y.CompareTo(b.Y);
    }

    private void ScanClassicMarkerNode(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layerProto,
        ClassicBiomeMarkerAreaScanState scan,
        Vector2i node,
        int depth)
    {
        var replaceable = GetClassicReplaceableMarkerEntity(biome, gridUid, grid, node, depth);
        var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
        EntityUid? existing = null;
        while (enumerator.MoveNext(out var anchored))
        {
            if (anchored == replaceable)
            {
                existing = anchored;
                continue;
            }

            existing = anchored;
            break;
        }

        if (!scan.Forced && existing != null && existing != replaceable)
            return;

        TryGetEntity(node, biome, (gridUid, grid), out var proto);
        if (layerProto.EntityMask.Count > 0 &&
            (proto == null || !layerProto.EntityMask.ContainsKey(proto)))
        {
            return;
        }

        if (proto != null && layerProto.Prototype != null)
            return;

        DebugTools.Assert(layerProto.EntityMask.Count == 0 || !string.IsNullOrEmpty(proto));
        scan.RemainingTiles.Add(node);
        scan.NodeEntities.Add(node, existing);
        scan.NodeMasks.Add(node, proto);
        if (replaceable != EntityUid.Invalid)
            scan.ReplaceableEntities.Add(node, replaceable);
    }

    private EntityUid GetClassicReplaceableMarkerEntity(
        BiomeComponent biome,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i node,
        int depth)
    {
        var chunk = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;
        if (!biome.LoadedEntities.TryGetValue(chunk, out var loaded))
            return EntityUid.Invalid;

        foreach (var (entity, tile) in loaded)
        {
            if (tile != node || Deleted(entity) ||
                !_xformQuery.TryGetComponent(entity, out var xform) || !xform.Anchored ||
                _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates) != node ||
                ClassicHasPersistentBiomeDependent(gridUid, grid, node, entity) ||
                !ClassicCanUnloadBiomeEntity(gridUid, entity, depth))
            {
                continue;
            }

            return entity;
        }

        return EntityUid.Invalid;
    }

    /// <summary>
    /// Advances the seeded selection used by <see cref="GetMarkerNodes"/> without changing its
    /// random draw order. HashSet seed lookup is deliberately resumed through its enumerator:
    /// Enumerable-style random selection can otherwise hide an O(area) walk in a single tick.
    /// </summary>
    private bool AdvanceClassicMarkerSelection(
        ClassicBiomeMarkerAreaScanState scan,
        BiomeMarkerLayerPrototype layerProto,
        int maxOperations,
        ref int processed)
    {
        while (scan.GroupIndex < scan.Count)
        {
            if (processed >= maxOperations)
                return false;

            if (!scan.GroupStarted)
            {
                scan.GroupRemaining = scan.Random.Next(layerProto.MinGroupSize, layerProto.MaxGroupSize + 1);
                scan.GroupStarted = true;
                processed++;

                if (processed >= maxOperations || ClassicStreamingBudgetExpired())
                    return false;
            }

            if (scan.GroupRemaining <= 0 ||
                scan.Frontier.Count == 0 && scan.RemainingTiles.Count == 0)
            {
                if (scan.GroupRemaining > 0)
                    Log.Warning("Found remaining group size for ore veins!");

                scan.Frontier.Clear();
                scan.GroupRemaining = 0;
                scan.GroupStarted = false;
                scan.GroupIndex++;
                processed++;

                if (processed >= maxOperations || ClassicStreamingBudgetExpired())
                    return scan.GroupIndex >= scan.Count;

                continue;
            }

            if (scan.Frontier.Count == 0)
            {
                if (!scan.SeedPickActive)
                {
                    scan.SeedPickTarget = scan.Random.Next(scan.RemainingTiles.Count);
                    scan.SeedPickCursor = 0;
                    scan.SeedEnumerator = scan.RemainingTiles.GetEnumerator();
                    scan.SeedPickActive = true;
                }

                if (!scan.SeedEnumerator.MoveNext())
                {
                    DisposeClassicMarkerScan(scan);
                    throw new InvalidOperationException("Classic marker seed enumeration ended before its selected index.");
                }

                var startNode = scan.SeedEnumerator.Current;
                var selected = scan.SeedPickCursor++ == scan.SeedPickTarget;
                processed++;
                if (selected)
                {
                    DisposeClassicMarkerScan(scan);
                    scan.RemainingTiles.Remove(startNode);
                    scan.Frontier.Clear();
                    scan.Frontier.Add(startNode);
                }

                if (processed >= maxOperations || ClassicStreamingBudgetExpired())
                    return false;

                continue;
            }

            var frontierIndex = scan.Random.Next(scan.Frontier.Count);
            var node = scan.Frontier[frontierIndex];
            scan.Frontier.RemoveSwap(frontierIndex);
            scan.RemainingTiles.Remove(node);

            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                var neighbor = new Vector2i(node.X + x, node.Y + y);
                if (scan.Frontier.Contains(neighbor) || !scan.RemainingTiles.Contains(neighbor))
                    continue;

                scan.Frontier.Add(neighbor);
            }

            var chunkOrigin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;
            scan.PendingMarkers.GetOrNew(chunkOrigin).GetOrNew(scan.Layer).Add(node);
            scan.GroupRemaining--;

            if (scan.NodeEntities.TryGetValue(node, out var existing) &&
                existing is { } existingUid &&
                (!scan.ReplaceableEntities.TryGetValue(node, out var replaceable) || existingUid != replaceable))
            {
                scan.EntitiesToDelete.Add(existingUid);
            }

            processed++;
            if (processed >= maxOperations || ClassicStreamingBudgetExpired())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Publishes a completed marker snapshot atomically. Until this point cancellation has no
    /// gameplay-visible effects, which is important because view requests are rebuilt each tick.
    /// </summary>
    private void PublishClassicMarkerAreaScan(
        BiomeComponent component,
        ClassicBiomeMarkerAreaScanState scan)
    {
        foreach (var entity in scan.EntitiesToDelete)
        {
            if (!Deleted(entity))
                Del(entity);
        }

        if (!component.LoadedMarkers.TryGetValue(scan.Layer, out var completed))
        {
            completed = new HashSet<Vector2i>();
            component.LoadedMarkers.Add(scan.Layer, completed);
        }
        completed.Add(scan.MarkerChunk);

        foreach (var (chunkOrigin, layers) in scan.PendingMarkers)
        {
            var destination = component.PendingMarkers.GetOrNew(chunkOrigin);
            foreach (var (layer, nodes) in layers)
                destination[layer] = nodes;
        }
    }

    /// <summary>
    /// Gets the marker nodes for the specified area.
    /// </summary>
    /// <param name="emptyTiles">Should we include empty tiles when determine markers (e.g. if they are yet to be loaded)</param>

    /// <summary>
    /// Applies at most <paramref name="maxNodes"/> precomputed marker nodes. Classic streaming
    /// uses the bounded form for already-loaded and background chunks; generic biomes retain the
    /// original all-at-once behavior through <see cref="LoadChunkMarkers"/>.
    /// </summary>
    private int LoadChunkMarkerNodes(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        int maxNodes)
    {
        if (maxNodes <= 0 || !component.PendingMarkers.TryGetValue(chunk, out var layers))
            return 0;

        if (!component.ModifiedTiles.TryGetValue(chunk, out var modified))
        {
            modified = _tilePool.Get();
            component.ModifiedTiles.Add(chunk, modified);
        }

        var processed = 0;
        var classicStreaming = _classicStreamingQuery.HasComp(gridUid);
        _classicMarkerLayers.Clear();
        foreach (var (layer, nodes) in layers)
        {
            var layerProto = ProtoManager.Index<BiomeMarkerLayerPrototype>(layer);
            var count = Math.Min(nodes.Count, maxNodes - processed);
            var consumed = 0;

            while (consumed < count)
            {
                var node = nodes[consumed];
                if (!modified.Contains(node))
                {
                    if (classicStreaming &&
                        !TryRemoveClassicMarkerBaseline(component, gridUid, grid, chunk, node))
                    {
                        modified.Add(node);
                    }
                    else
                    {
                        if (TryGetBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                        {
                            SetClassicBiomeTile(gridUid, grid, node, tile.Value);
                        }

                        string? prototype;

                        if (TryGetEntity(node, component, (gridUid, grid), out var proto) &&
                            layerProto.EntityMask.TryGetValue(proto, out var maskedProto))
                        {
                            prototype = maskedProto;
                        }
                        else
                        {
                            prototype = layerProto.Prototype;
                        }

                        var uid = EntityManager.CreateEntityUninitialized(prototype, _mapSystem.GridTileToLocal(gridUid, grid, node));
                        RemComp<GhostTakeoverAvailableComponent>(uid);
                        RemComp<GhostRoleComponent>(uid);
                        EntityManager.InitializeAndStartEntity(uid);
                        modified.Add(node);
                    }
                }

                consumed++;
                if (classicStreaming && ClassicStreamingBudgetExpired())
                    break;
            }

            if (consumed > 0)
            {
                nodes.RemoveRange(0, consumed);
                processed += consumed;
            }

            if (nodes.Count == 0)
                _classicMarkerLayers.Add(layer);
            if (processed >= maxNodes || classicStreaming && ClassicStreamingBudgetExpired())
                break;
        }

        foreach (var layer in _classicMarkerLayers)
            layers.Remove(layer);

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
            _tilePool.Return(modified);
        }

        if (layers.Count == 0)
            component.PendingMarkers.Remove(chunk);

        return processed;
    }


    /// <summary>
    /// Loads a contiguous range of cells in a biome chunk. Classic background streaming uses
    /// this to keep a single 8x8 wall chunk from monopolizing one server tick.
    /// </summary>
    private int LoadChunkCells(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        int startCell,
        int cellCount,
        bool stopWhenClassicBudgetExpires = false)
    {
        var totalCells = ChunkSize * ChunkSize;
        startCell = Math.Clamp(startCell, 0, totalCells);
        var endCell = Math.Min(totalCells, startCell + Math.Clamp(cellCount, 0, totalCells));
        if (startCell >= endCell)
            return startCell;

        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        _tiles.Clear();
        Array.Clear(_chunkBiomeTiles);
        var cacheClassicTiles = _classicStreamingQuery.HasComp(gridUid);
        var appliedTiles = false;

        using (_classicProfiler.Value("ClassicBiome.TileSlice"))
        {
            for (var cell = startCell; cell < endCell; cell++)
            {
                var x = cell / ChunkSize;
                var y = cell % ChunkSize;
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                if (_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                    continue;

                if (!TryGetTile(indices, component.Layers, seed, (gridUid, grid), out var biomeTile))
                    continue;

                if (cacheClassicTiles)
                    _chunkBiomeTiles[cell] = biomeTile.Value;
                _tiles.Add((indices, biomeTile.Value));
            }

            appliedTiles = _tiles.Count > 0;
            using (_classicProfiler.Value("ClassicBiome.TileApply"))
                SetClassicBiomeTiles(gridUid, grid, _tiles);
            _tiles.Clear();
        }

        if (stopWhenClassicBudgetExpires && appliedTiles && ClassicStreamingBudgetExpired())
            return startCell;

        var processedEnd = endCell;
        if (ClassicBiomeLoadsEntities(gridUid))
        {
            using (_classicProfiler.Value("ClassicBiome.EntitySlice"))
            {
                component.LoadedEntities.TryGetValue(chunk, out var loadedEntities);
                processedEnd = startCell;

                for (var cell = startCell; cell < endCell; cell++)
                {
                    var x = cell / ChunkSize;
                    var y = cell % ChunkSize;
                    var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                    if (modified.Contains(indices))
                        goto CellComplete;

                    if (loadedEntities != null && loadedEntities.ContainsValue(indices))
                        goto CellComplete;

                    var biomeTile = _chunkBiomeTiles[cell];
                    var hasEntity = biomeTile is { } tile
                        ? TryGetEntity(indices, component.Layers, tile, seed, (gridUid, grid), out var entPrototype)
                        : TryGetEntity(indices, component, (gridUid, grid), out entPrototype);
                    if (!hasEntity)
                        goto CellComplete;

                    var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
                    if (anchored.MoveNext(out _))
                        goto CellComplete;

                    EntityUid ent;
                    using (_classicProfiler.Value("ClassicBiome.EntitySpawn"))
                        ent = Spawn(entPrototype, _mapSystem.GridTileToLocal(gridUid, grid, indices));

                    if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                    {
                        _transform.AnchorEntity((ent, xform), (gridUid, grid), indices);
                    }

                    if (loadedEntities == null)
                    {
                        loadedEntities = new Dictionary<EntityUid, Vector2i>(ChunkSize * ChunkSize);
                        component.LoadedEntities.Add(chunk, loadedEntities);
                    }
                    loadedEntities.Add(ent, indices);

                    CellComplete:
                    processedEnd = cell + 1;
                    if (stopWhenClassicBudgetExpires && ClassicStreamingBudgetExpired())
                        break;
                }
            }
        }

        if (ClassicBiomeLoadsDecals(gridUid))
        {
            using (_classicProfiler.Value("ClassicBiome.DecalSlice"))
            {
                component.LoadedDecals.TryGetValue(chunk, out var loadedDecals);

                for (var cell = startCell; cell < processedEnd; cell++)
                {
                    var x = cell / ChunkSize;
                    var y = cell % ChunkSize;
                    var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                    if (modified.Contains(indices))
                        continue;

                    if (loadedDecals != null && loadedDecals.ContainsValue(indices))
                        continue;

                    var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

                    if (anchored.MoveNext(out _) || !TryGetDecals(indices, component.Layers, seed, (gridUid, grid), out var decals))
                        continue;

                    foreach (var decal in decals)
                    {
                        if (!_decals.TryAddDecal(decal.ID, new EntityCoordinates(gridUid, decal.Position), out var dec))
                            continue;

                        if (loadedDecals == null)
                        {
                            loadedDecals = new Dictionary<uint, Vector2i>();
                            component.LoadedDecals.Add(chunk, loadedDecals);
                        }
                        loadedDecals.Add(dec, indices);
                    }
                }
            }
        }

        if (modified.Count == 0)
        {
            _tilePool.Return(modified);
            component.ModifiedTiles.Remove(chunk);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }

        return processedEnd;
    }


}
