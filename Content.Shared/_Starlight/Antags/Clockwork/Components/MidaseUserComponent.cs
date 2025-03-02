using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedMidaseSystem))]
[AutoGenerateComponentState]
public sealed partial class MidaseUserComponent : Component
{
    [DataField]
    public SpriteSpecifier MidaseVisuals = new SpriteSpecifier.Rsi(new ("/Textures/_Starlight/Effects/midase.rsi"), "effect");
    
    [DataField]
    public int LayerId = 0;
    
    [DataField, AutoNetworkedField]
    public bool MidaseEnabled = false;
    
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string MidaseToggleAction = "ActionMidaseToggle";
    
    [DataField] 
    public EntityUid? MidaseToggleActionEntity;
}