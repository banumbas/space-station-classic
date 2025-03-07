using Robust.Shared.GameStates;
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
    public int rangeLimit = 10;
    
    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(60);
}