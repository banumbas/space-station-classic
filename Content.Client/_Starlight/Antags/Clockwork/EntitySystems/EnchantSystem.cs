using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Client._Starlight.Antags.Clockwork.UI;
using Robust.Shared.Timing;

namespace Content.Client.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class EnchantSystem : SharedEnchantSystem
{
        [Dependency] private readonly IGameTiming _timing = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantUserComponent, ClockworkItemEnchantEvent>(OnEnchant);
        base.Initialize();
    }
    
    private void OnEnchant(EntityUid uid, EnchantUserComponent component, ClockworkItemEnchantEvent args)
    {
        if (_timing.IsFirstTimePredicted)
            new EnchantBoundUserInterface(uid, EnchantUIKey.Key);
    }
}