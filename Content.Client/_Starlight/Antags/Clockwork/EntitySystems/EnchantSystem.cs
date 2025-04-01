using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Client._Starlight.Antags.Clockwork.UI;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;

namespace Content.Client.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class EnchantSystem : SharedEnchantSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantableComponent, GotEquippedHandEvent>(OnHandEquip);
        SubscribeLocalEvent<EnchantableComponent, GotUnequippedHandEvent>(OnHandUnequip);
        SubscribeLocalEvent<EnchantableComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<EnchantableComponent, GotUnequippedEvent>(OnUnequip);
        SubscribeLocalEvent<EnchantUserComponent, ClockworkItemEnchantEvent>(OnEnchant);
    }
    
    private void OnHandEquip(EntityUid uid, EnchantableComponent component, GotEquippedHandEvent args)
    {
        base.EquipItem(uid, args.User);
        UpdateUI(args.User);
    }
    
    private void OnEquip(EntityUid uid, EnchantableComponent component, GotEquippedEvent args)
    {
        base.EquipItem(uid, args.Equipee);
        UpdateUI(args.Equipee);
    }
    
    private void OnHandUnequip(EntityUid uid, EnchantableComponent component, GotUnequippedHandEvent args)
    {
        base.RemoveItem(uid, args.User);
        UpdateUI(args.User);
    }
    
    private void OnUnequip(EntityUid uid, EnchantableComponent component, GotUnequippedEvent args)
    {
        base.RemoveItem(uid, args.Equipee);
        UpdateUI(args.Equipee);
    }
    
    private void OnEnchant(EntityUid uid, EnchantUserComponent component, ClockworkItemEnchantEvent args)
    {
        if (_timing.IsFirstTimePredicted)
        {
            var boundUserInterface = new EnchantBoundUserInterface(uid, EnchantUIKey.Key);
            boundUserInterface.ToggleWindow();
        }
    }
    
    private void UpdateUI(EntityUid target)
    {
        if (_ui.TryGetOpenUi<EnchantBoundUserInterface>(target, EnchantUIKey.Key, out var bui))
            bui.Update();
    }
}