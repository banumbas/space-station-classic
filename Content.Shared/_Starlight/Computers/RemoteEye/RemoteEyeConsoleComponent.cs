using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Actions;
using Content.Shared.Starlight.Antags.Abductor;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Computers.RemoteEye;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRemoteEyeSystem)), AutoGenerateComponentState]
public sealed partial class RemoteEyeConsoleComponent : Component
{
    [DataField(readOnly: true)]
    public EntProtoId RemoteEntityProto;

    [DataField, AutoNetworkedField]
    public NetEntity? RemoteEntity;

    [DataField(readOnly: true)]
    public ComponentRegistry? RequiredComponents;

    [DataField(readOnly: true)]
    public EntProtoId<InstantActionComponent>[] Actions = [];

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("color")]
    public Color Color { get; set; } = Color.White;
}
