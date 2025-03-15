using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BrassBeaconComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public int range = 0;
    
    [DataField]
    public int rangeLimit = 40;

    public HashSet<Entity<BeaconTransformableComponent>> EntitiesToTransform = new();
    
    public HashSet<TileRef> TilesToTransform = new();
    
    [DataField]
    public EntProtoId? EffectProto = null;
    
    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);
    
    [DataField]
    public int TransformedCount = 0;
    
    [DataField]
    public EntProtoId BatteryProtoId = "ClockworkBattery";
}