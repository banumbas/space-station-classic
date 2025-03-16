using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared._Starlight.StateMachines;
using Robust.Shared.Spawners;

namespace Content.Server._Starlight.StateMachines;
public sealed partial class EntityStateMachineSystem : SharedEntityStateMachineSystem
{
    public void SetLifeTime(Entity<EntityStateMachineComponent> entity, float lifeTime)
    {
        var tempLifeTime = lifeTime;

        if (entity.Comp.Lifetimes.TryGetValue(EntityStateMachine.Spawn, out var spawnLifeTime))
            tempLifeTime -= spawnLifeTime;

        if (entity.Comp.Lifetimes.TryGetValue(EntityStateMachine.Despawn, out var despawnLifeTime))
            tempLifeTime -= despawnLifeTime;

        entity.Comp.Lifetimes[EntityStateMachine.Idle] = tempLifeTime;

        var despawn = EnsureComp<TimedDespawnComponent>(entity.Owner);
        despawn.Lifetime = lifeTime;
    }
}
