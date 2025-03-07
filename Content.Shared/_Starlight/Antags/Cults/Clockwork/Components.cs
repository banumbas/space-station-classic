using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Cults.Clockwork;
[RegisterComponent, NetworkedComponent]
public sealed partial class ClockworkCultistComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "ClockworkCultistFaction";
}
[RegisterComponent, NetworkedComponent]
public sealed partial class ClockworkMasterComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "ClockworkMasterFaction";
}
