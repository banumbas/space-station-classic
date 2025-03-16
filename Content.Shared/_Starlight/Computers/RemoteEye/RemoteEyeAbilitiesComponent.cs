using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Computers.RemoteEye;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedRemoteEyeSystem))]
public sealed partial class RemoteEyeActorComponent : Component
{
    [DataField]
    public EntityUid[] HiddenActions = [];

    [DataField]
    public EntityUid? VirtualItem;

    public EntityUid?[] ActionsEntities = [];
}
