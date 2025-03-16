using System;
using System.Linq;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Content.Shared.Anomaly.Components;
using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StateMachines;
public abstract partial class SharedEntityStateMachineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStateMachineComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<EntityStateMachineComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.LastUpdate = _timing.RealTime;
        ent.Comp.CurrentState = EntityStateMachine.Spawn;
        if(TryComp<AppearanceComponent>(ent, out var appearance)) 
        _appearance.SetData(ent, EntityStateMachineApperance.State, ent.Comp.CurrentState, appearance);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.RealTime;

        var query = EntityQueryEnumerator<EntityStateMachineComponent, AppearanceComponent>();
        while (query.MoveNext(out var ent, out var stateMachine, out var appearance))
        {
            if (stateMachine.CurrentState == EntityStateMachine.Despawn
                || !stateMachine.Lifetimes.TryGetValue(stateMachine.CurrentState, out var lifetime)
                || TimeSpan.FromSeconds(lifetime) + stateMachine.LastUpdate >= time)
                continue;

            stateMachine.LastUpdate = time;
            stateMachine.CurrentState++;
            _appearance.SetData(ent, EntityStateMachineApperance.State, stateMachine.CurrentState, appearance);
        }
    }
}
[Serializable, NetSerializable]
public enum EntityStateMachineApperance : byte
{
    State
}