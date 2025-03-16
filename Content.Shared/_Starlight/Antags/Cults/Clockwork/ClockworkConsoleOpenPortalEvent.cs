using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Cults.Clockwork;

public sealed partial class ClockworkConsoleOpenPortalEvent : WorldTargetActionEvent
{
    [DataField("spot")]
    public EntProtoId Spot = "ClockworkArch";

    [DataField("portal")]
    public EntProtoId Portal = "ClockworkRitualPortal";

    [DataField("effect")]
    public EntProtoId? Effect;
}