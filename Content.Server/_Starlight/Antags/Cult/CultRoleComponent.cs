using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Roles;

namespace Content.Server._Starlight.Antags.Cult;
[RegisterComponent]
public sealed partial class CultRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public string Briefing = "clockwork-briefing";
}
[RegisterComponent]
public sealed partial class CultLeaderRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public string Briefing = "clockwork-master-briefing";
}
