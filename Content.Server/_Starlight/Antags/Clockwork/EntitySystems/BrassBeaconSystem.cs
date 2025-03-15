using System.Numerics;
using System.Linq;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class BrassBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly ITileDefinitionManager _tiledef = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        var query = EntityQueryEnumerator<BrassBeaconComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.range >= component.rangeLimit)
                continue;
            
            if (component.NextUpdateTime == TimeSpan.FromSeconds(0))
                component.NextUpdateTime = Timing.CurTime;
            
            if (Timing.CurTime < component.NextUpdateTime)
                continue;

            component.NextUpdateTime += component.Delay;
            
            var xform = Transform(uid);
            var centerCoords = xform.Coordinates;
            
            bool EntitiesEnough = component.EntitiesToTransform.Count > 6;
            if (component.TilesToTransform.Count < 6 || !EntitiesEnough)
            {
                if (component.TilesToTransform.Count < 6 && !EntitiesEnough)
                    component.range = Math.Min(component.rangeLimit, component.range + 2);
                
                if (!EntitiesEnough)
                    component.EntitiesToTransform = _lookup.GetEntitiesInRange<BeaconTransformableComponent>(centerCoords, component.range, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);
                
                if (component.TilesToTransform.Count > 6 || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
                    return;
                
                HashSet<TileRef> TilesToTransform = _map.GetLocalTilesIntersecting(xform.GridUid.Value, grid, new Box2(centerCoords.Position + new Vector2(-component.range, -component.range), centerCoords.Position + new Vector2(component.range, component.range))).ToHashSet();
                
                foreach (var tile in TilesToTransform)
                    if (tile.Tile.GetContentTileDefinition().BrassToTransform is { } brassToTransform)
                        component.TilesToTransform.Add(tile);
            }
            
            if ((_random.Prob(0.5f) || !EntitiesEnough) && component.TilesToTransform.Count >= 6)
            {
                var tile = _random.Pick(component.TilesToTransform);
                component.TilesToTransform.Remove(tile);
                
                var tileProto = tile.Tile.GetContentTileDefinition();
                if (tileProto.BrassToTransform is { } brassToTransform)
                {
                    //if (tileProto.TransformEffectProto != null)
                    //    Spawn(tileProto.TransformEffectProto.Value, Transform(entity).Coordinates); Someday
                    component.TransformedCount++;
                    var trasformTo = (ContentTileDefinition) _tiledef[brassToTransform];
                    _tile.ReplaceTile(tile, trasformTo);
                }
            }
            else
            {
                var entity = _random.Pick(component.EntitiesToTransform);
                component.EntitiesToTransform.Remove(entity);
                
                if (TryComp<BeaconTransformableComponent>(entity, out var transformTo) && transformTo.TargetEntity != null)
                {
                    component.TransformedCount++;
                    Spawn(transformTo.TargetEntity.Value, Transform(entity).Coordinates);
                    if (transformTo.EffectProto != null)
                        Spawn(transformTo.EffectProto.Value, Transform(entity).Coordinates);
                    QueueDel(entity);
                }
            }
            
            if (component.TransformedCount % 5 == 0)
                Spawn(component.BatteryProtoId, centerCoords);
        }
    }
}