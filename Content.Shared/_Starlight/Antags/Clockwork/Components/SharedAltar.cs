using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

[Serializable, NetSerializable]
public sealed partial class AltarConvertationDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum ClockworkAltarVisuals : byte
{
    Enabled
}

[Serializable, NetSerializable]
public enum ClockworkAltarVisualLayers : byte
{
    Base,
    Glow
}