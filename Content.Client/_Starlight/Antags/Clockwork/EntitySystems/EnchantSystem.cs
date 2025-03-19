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
        base.Initialize();
        SubscribeLocalEvent<EnchantUserComponent, ClockworkItemEnchantEvent>(OnEnchant);
    }
    
    public override void EquipItem(EntityUid uid, EntityUid user)
    {
        base.EquipItem(uid, user);
        UpdateUI(user);
    }

    public override void RemoveItem(EntityUid uid, EntityUid user)
    {
        base.RemoveItem(uid, user);
        UpdateUI(user);
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