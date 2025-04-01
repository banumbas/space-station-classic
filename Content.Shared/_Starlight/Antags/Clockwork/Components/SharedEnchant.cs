using JetBrains.Annotations;
using Content.Shared.Actions;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;

namespace Content.Shared.Starlight.Antags.Clockwork.Components;

public sealed partial class ClockworkItemEnchantEvent : InstantActionEvent
{

}

[Serializable, NetSerializable]
public sealed class ClockworkItemSelectedMessage : BoundUserInterfaceMessage
{
    public NetEntity Item = default!;
}

[Serializable, NetSerializable]
public sealed class ClockworkEnchantMessage : BoundUserInterfaceMessage
{
    public NetEntity Item = default!;
    public EnchantAction Action = default!;
    
    public ClockworkEnchantMessage(NetEntity item, EnchantAction action)
    {
        Item = item;
        Action = action;
    }
}

[Serializable, NetSerializable]
public enum EnchantUIKey
{
    Key
}

[Serializable, NetSerializable]
public enum EnchantVisuals
{
    Base,
    Overlay
}

/// <summary>
/// Used to do something after enchanting. Like give a invisibility or something.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
[Serializable, NetSerializable]
public partial class EnchantAction
{
    protected IEntityManager? entMan;
    
    [DataField("icon")]
    public SpriteSpecifier? Icon;
    
    [DataField("overlayState")]
    public string? ItemOverlayState;
    
    [DataField("activeState")]
    public string? ActiveState;
    
    public virtual void Action(EnchantActionArgs args)
    {
        entMan = args.EntityManager;
        
        Logger.DebugS("EnchantSystem", "Trying to enchant item");
        
        var _appearance = entMan.System<SharedAppearanceSystem>();
        
        if (ActiveState != null)
            _appearance.SetData(args.Item, EnchantVisuals.Base, ActiveState);
        
        if (ItemOverlayState != null)
            _appearance.SetData(args.Item, EnchantVisuals.Overlay, ItemOverlayState);
    }
    
    public virtual void Attack(EntityUid User, EntityUid Weapon, IReadOnlyList<EntityUid> HitEntities)
    {
        if (entMan == null)
            return;
        
        var _appearance = entMan.System<SharedAppearanceSystem>();
        
        if (entMan.TryGetComponent<EnchantableComponent>(Weapon, out var enchantable) && enchantable.BaseState != null)
            _appearance.SetData(Weapon, EnchantVisuals.Base, enchantable.BaseState);
    }
}
 
public readonly record struct EnchantActionArgs(EntityUid Enchanter, EntityUid Item, IEntityManager EntityManager);