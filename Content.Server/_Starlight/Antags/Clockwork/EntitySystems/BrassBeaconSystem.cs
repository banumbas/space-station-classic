using System.Numerics;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class BrassBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    
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
            
            var centerCoords = Transform(uid).Coordinates;
            
            if (component.EntitiesToTransform.Count < 6)
            {
                component.range = Math.Min(component.rangeLimit, component.range + 2);
                component.EntitiesToTransform = _lookup.GetEntitiesInRange<BeaconTransformableComponent>(centerCoords, component.range, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);
            }
            
            var entity = _random.Pick(component.EntitiesToTransform);
            component.EntitiesToTransform.Remove(entity);
                
            if (TryComp<BeaconTransformableComponent>(entity, out var transformTo) && transformTo.TargetEntity != null)
            {
                component.TransformedCount++;
                Spawn(transformTo.TargetEntity.Value, Transform(entity).Coordinates);
                QueueDel(entity);
            }
            
            if (component.TransformedCount % 5 == 0)
                Spawn(component.BatteryProtoId, centerCoords);
        }
    }
}