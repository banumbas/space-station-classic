using System.Linq;
using Content.Server._Classic.GameTicking.Rules.Components;
using Content.Server._Classic.Station;
using Content.Server._Classic.ZLevels.Core;
using Content.Server._Classic.ZLevels.Core.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._Classic.GameTicking.Rules;

/// <summary>
/// Handles data-driven game rules that merge map content into an existing Classic station Z-level.
/// </summary>
public sealed partial class ClassicMergeMapRuleSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ClassicStationBiomeSystem _stationBiome = default!;
    [Dependency] private ClassicZLevelsSystem _zLevels = default!;

    private readonly HashSet<EntityUid> _pendingRules = [];
    private readonly Dictionary<MergedMapKey, LoadedMapData> _loadedMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicMergeMapRuleComponent, GameRuleAddedEvent>(OnRuleAdded);
        SubscribeLocalEvent<ClassicMergeMapRuleComponent, GameRuleEndedEvent>(OnRuleEnded);
        SubscribeLocalEvent<ClassicZLevelMapNetworkReadyEvent>(OnMapNetworkReady);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRuleAdded(Entity<ClassicMergeMapRuleComponent> ent, ref GameRuleAddedEvent args)
    {
        if (ent.Owner == EntityUid.Invalid || !Exists(ent.Owner))
            return;

        _pendingRules.Add(ent.Owner);
        TryMergeRuleMap(ent, null);
    }

    private void OnRuleEnded(Entity<ClassicMergeMapRuleComponent> ent, ref GameRuleEndedEvent args)
    {
        if (ent.Owner != EntityUid.Invalid)
            _pendingRules.Remove(ent.Owner);
    }

    private void OnMapNetworkReady(ClassicZLevelMapNetworkReadyEvent args)
    {
        if (args.Network == EntityUid.Invalid || _pendingRules.Count == 0)
            return;

        foreach (var ruleUid in _pendingRules.ToArray())
        {
            if (ruleUid == EntityUid.Invalid ||
                !TryComp<ClassicMergeMapRuleComponent>(ruleUid, out var component))
            {
                _pendingRules.Remove(ruleUid);
                continue;
            }

            TryMergeRuleMap((ruleUid, component), args.Network);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _pendingRules.Clear();
        _loadedMaps.Clear();
    }

    private bool TryMergeRuleMap(Entity<ClassicMergeMapRuleComponent> rule, EntityUid? requiredNetwork)
    {
        if (rule.Owner == EntityUid.Invalid || !Exists(rule.Owner))
            return false;

        if (!TryFindTargetMap(
                rule.Comp.TargetDepth,
                requiredNetwork,
                out var stationUid,
                out var networkUid,
                out var network,
                out var mapId))
        {
            return false;
        }

        var key = new MergedMapKey(mapId, rule.Comp.MapPath);
        if (_loadedMaps.TryGetValue(key, out var loaded))
        {
            if (!TryGenerateBiomeGround(rule, stationUid, (networkUid, network), loaded.Grids))
                return false;

            NotifyRule(rule.Owner, loaded.MapId, loaded.Grids);
            return true;
        }

        if (!_mapLoader.TryMergeMap(mapId, rule.Comp.MapPath, out var grids))
        {
            Log.Error($"Failed to merge rule map {rule.Comp.MapPath} into Classic Z-depth " +
                      $"{rule.Comp.TargetDepth} on map {mapId}.");
            _pendingRules.Remove(rule.Owner);
            _ticker.EndGameRule(rule.Owner);
            return false;
        }

        var gridUids = new List<EntityUid>(grids.Count);
        foreach (var grid in grids)
        {
            if (grid.Owner != EntityUid.Invalid)
                gridUids.Add(grid.Owner);
        }

        var data = new LoadedMapData(mapId, gridUids);
        _loadedMaps.Add(key, data);

        if (!TryGenerateBiomeGround(rule, stationUid, (networkUid, network), data.Grids))
            return false;

        NotifyRule(rule.Owner, data.MapId, data.Grids);
        return true;
    }

    private bool TryGenerateBiomeGround(
        Entity<ClassicMergeMapRuleComponent> rule,
        EntityUid stationUid,
        Entity<ClassicZMapNetworkComponent?> network,
        IReadOnlyList<EntityUid> grids)
    {
        if (rule.Comp.BiomeGroundDepth is not { } groundDepth)
            return true;

        if (!_zLevels.TryGetMapAtDepth(network, groundDepth, out var groundMapUid) ||
            groundMapUid == EntityUid.Invalid ||
            !TryComp<MapComponent>(groundMapUid, out var groundMap) ||
            !_map.MapExists(groundMap.MapId) ||
            !_stationBiome.TryGenerateBiomeGround(stationUid, groundMap.MapId, grids))
        {
            Log.Error($"Failed to generate biome ground for rule map {rule.Comp.MapPath} " +
                      $"at Classic Z-depth {groundDepth}.");
            _pendingRules.Remove(rule.Owner);
            _ticker.EndGameRule(rule.Owner);
            return false;
        }

        return true;
    }

    private bool TryFindTargetMap(
        int targetDepth,
        EntityUid? requiredNetwork,
        out EntityUid stationUid,
        out EntityUid networkUid,
        out ClassicZMapNetworkComponent network,
        out MapId mapId)
    {
        stationUid = EntityUid.Invalid;
        networkUid = EntityUid.Invalid;
        network = default!;
        mapId = MapId.Nullspace;

        var stations = EntityQueryEnumerator<ClassicStationZLevelsComponent>();
        while (stations.MoveNext(out var candidateStationUid, out var station))
        {
            if (station.ZNetworkEntity is not { } candidateNetworkUid ||
                candidateNetworkUid == EntityUid.Invalid ||
                requiredNetwork is { } required && candidateNetworkUid != required ||
                !TryComp<ClassicZMapNetworkComponent>(candidateNetworkUid, out var candidateNetwork) ||
                !_zLevels.TryGetMapAtDepth((candidateNetworkUid, candidateNetwork), targetDepth, out var mapUid) ||
                mapUid == EntityUid.Invalid ||
                !TryComp<MapComponent>(mapUid, out var mapComponent) ||
                !_map.MapExists(mapComponent.MapId))
            {
                continue;
            }

            stationUid = candidateStationUid;
            networkUid = candidateNetworkUid;
            network = candidateNetwork;
            mapId = mapComponent.MapId;
            return true;
        }

        return false;
    }

    private void NotifyRule(EntityUid ruleUid, MapId mapId, IReadOnlyList<EntityUid> grids)
    {
        _pendingRules.Remove(ruleUid);

        if (ruleUid == EntityUid.Invalid || !Exists(ruleUid))
            return;

        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(ruleUid, ref ev);
    }

    private readonly record struct MergedMapKey(MapId MapId, ResPath MapPath);
    private sealed record LoadedMapData(MapId MapId, IReadOnlyList<EntityUid> Grids);
}
