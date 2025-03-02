using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, Access(typeof(SharedMidaseSystem))]
public sealed partial class MidaseTransformableComponent : Component
{
    [DataField("targetEntity")]
    public EntProtoId? TargetEntity;
    
    [DataField]
    public bool TransformStack = false;
}