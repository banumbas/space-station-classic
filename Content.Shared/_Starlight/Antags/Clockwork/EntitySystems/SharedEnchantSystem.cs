using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Starlight.Antags.Clockwork.EntitySystems;

public abstract class SharedEnchantSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantableComponent, GotEquippedHandEvent>(OnHandEquip);
        SubscribeLocalEvent<EnchantableComponent, GotUnequippedHandEvent>(OnHandUnequip);
        SubscribeLocalEvent<EnchantableComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<EnchantableComponent, GotUnequippedEvent>(OnUnequip);
        base.Initialize();
    }
    
    private void OnHandEquip(EntityUid uid, EnchantableComponent component, GotEquippedHandEvent args)
    {
        EquipItem(uid, args.User);
    }
    
    private void OnEquip(EntityUid uid, EnchantableComponent component, GotEquippedEvent args)
    {
        EquipItem(uid, args.Equipee);
    }
    
    public void EquipItem(EntityUid uid, EntityUid user)
    {
        if (TryComp<EnchantUserComponent>(user, out var enchantUser))
        {
            enchantUser.EntitiesToEnchant.Add(uid);
            if (enchantUser.EnchantActionEntity == null)
                _action.AddAction(user, ref enchantUser.EnchantActionEntity, enchantUser.EnchantAction);
        }
    }
    
    private void OnHandUnequip(EntityUid uid, EnchantableComponent component, GotUnequippedHandEvent args)
    {
        RemoveItem(uid, args.User);
    }
    
    private void OnUnequip(EntityUid uid, EnchantableComponent component, GotUnequippedEvent args)
    {
        RemoveItem(uid, args.Equipee);
    }
    
    public void RemoveItem(EntityUid uid, EntityUid user)
    {
        if (TryComp<EnchantUserComponent>(user, out var enchantUser))
        {
            enchantUser.EntitiesToEnchant.Remove(uid);
            if (enchantUser.EntitiesToEnchant.Count == 0 && enchantUser.EnchantActionEntity != null)
            {
                _action.RemoveAction(user, enchantUser.EnchantActionEntity);
                _actionContainer.RemoveAction(enchantUser.EnchantActionEntity.Value);
                enchantUser.EnchantActionEntity = null;
            }
        }
    }
}