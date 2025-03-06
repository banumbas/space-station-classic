using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAltarSystem))]
public sealed partial class AltarComponent : Component
{
}