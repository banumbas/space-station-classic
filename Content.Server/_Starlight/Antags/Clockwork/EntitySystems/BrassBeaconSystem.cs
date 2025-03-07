using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class BrassBeaconSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        var query = EntityQueryEnumerator<BrassBeaconComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.NextUpdateTime == TimeSpan.FromSeconds(0))
                component.NextUpdateTime = Timing.CurTime;
            
            if (Timing.CurTime < component.NextUpdateTime)
                continue;

            component.NextUpdateTime += component.Delay;
            
            if (component.range < component.rangeLimit)
                component.range++;
            
            // Get Entities in range and transform
            foreach (var entity in _lookup.GetEntitiesInRange(uid, component.range, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries))
            {
                if (TryComp<BeaconTransformableComponent>(entity, out var transformTo) && transformTo.TargetEntity != null)
                {
                    Spawn(transformTo.TargetEntity.Value, Transform(entity).Coordinates);
                    QueueDel(entity);
                }
            }
        }
    }
}