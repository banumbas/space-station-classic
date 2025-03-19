using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent]
public sealed partial class EnchantableComponent : Component
{
    [DataField("actions", serverOnly: true)]
    [NonSerialized]
    public List<EnchantAction> Actions = new();
    
    [DataField]
    public string? BaseState;
}