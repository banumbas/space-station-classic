using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Abstract;
[RegisterComponent, NetworkedComponent]
public sealed partial class ReferenceComponent: Component
{
    [DataField]
    public EntityUid? Reference;
}