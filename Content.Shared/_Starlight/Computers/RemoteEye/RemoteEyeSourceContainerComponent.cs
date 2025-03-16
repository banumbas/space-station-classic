using Content.Shared.Starlight.Antags.Abductor;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Computers.RemoteEye;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedRemoteEyeSystem)), AutoGenerateComponentState]
public sealed partial class RemoteEyeSourceContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Actor;
}