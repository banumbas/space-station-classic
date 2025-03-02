using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

public sealed partial class MidaseToggleEvent : InstantActionEvent
{

}

[Serializable, NetSerializable]
public enum MidaseVisuals : byte
{
    Enabled
}