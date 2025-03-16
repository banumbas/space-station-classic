using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.StateMachines;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EntityStateMachineComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LastUpdate = TimeSpan.MinValue;

    [DataField("lifetimes")]
    public Dictionary<EntityStateMachine, float> Lifetimes = [];

    [DataField, AutoNetworkedField]
    public EntityStateMachine CurrentState = EntityStateMachine.Spawn;
}

[Serializable, NetSerializable]
public enum EntityStateMachine : byte
{
    Spawn,
    Idle,
    Despawn,
}
public static class EntityStateMachineExtensions
{
    public static EntityStateMachine[] LifeTime =>
    [
        EntityStateMachine.Spawn,
        EntityStateMachine.Idle,
        EntityStateMachine.Despawn
    ];  
}