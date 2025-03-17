using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

public sealed partial class ClockworkItemEnchantEvent : InstantActionEvent
{

}

[Serializable, NetSerializable]
public sealed class ClockworkEnchantMessage : BoundUserInterfaceMessage
{
    public BaseClockworkEnchantAction Event = default!;
}

[Serializable, NetSerializable]
public abstract class BaseClockworkEnchantAction
{
    [field:NonSerialized]
    public EntityUid User { get; set; }
}

[Serializable, NetSerializable]
public enum EnchantUIKey
{
    Key
}