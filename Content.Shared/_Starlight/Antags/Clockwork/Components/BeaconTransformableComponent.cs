using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BeaconTransformableComponent : Component
{
    [DataField("targetEntity")]
    public EntProtoId? TargetEntity;
    
    [DataField]
    public EntProtoId? EffectProto = null;
}