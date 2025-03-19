using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class EnchantedComponent : Component
{
     [DataField("action")]
     [NonSerialized]
     public EnchantAction? Action;
     
     [DataField]
     public bool RiseActionOnAttack = false;
}