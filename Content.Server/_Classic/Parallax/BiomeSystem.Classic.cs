// Namespace does not match folder structure
#pragma warning disable IDE0130
using System.Diagnostics.CodeAnalysis;
using Content.Server._Classic.Geyser;
using Content.Server._Classic.Station;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

/// <summary>
/// Classic-specific biome streaming fast paths.
/// </summary>
public sealed partial class BiomeSystem
{
    [Dependency] private EntityQuery<ClassicUndergroundBiomeComponent> _classicUndergroundQuery = default!;
    [Dependency] private EntityQuery<ClassicBiomeAlwaysUnloadComponent> _classicAlwaysUnloadQuery = default!;
    [Dependency] private EntityQuery<ClassicGeyserGeneratorComponent> _classicGeyserGeneratorQuery = default!;

    private readonly HashSet<EntityUid> _classicBiomeTileWrites = new();

    private void InitializeClassicBiome()
    {
        SubscribeLocalEvent<BiomeComponent, TileChangedEvent>(OnClassicBiomeTileChanged);
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

    private bool ClassicCanUnloadMutatedBiomeEntity(EntityUid uid)
    {
        return _classicAlwaysUnloadQuery.HasComponent(uid);
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
