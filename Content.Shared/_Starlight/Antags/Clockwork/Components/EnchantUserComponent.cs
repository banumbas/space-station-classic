using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class EnchantUserComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EnchantAction = "ActionItemEnchant";
    
    public HashSet<EntityUid> EntitiesToEnchant = new();
    
    [DataField] 
    public EntityUid? EnchantActionEntity;
}