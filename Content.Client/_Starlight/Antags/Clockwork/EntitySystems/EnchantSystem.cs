using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;

namespace Content.Client.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class EnchantSystem : SharedEnchantSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantableComponent, ClockworkItemEnchantEvent>(OnEnchant);
        base.Initialize();
    }
    
    private void OnEnchant(EntityUid uid, EnchantableComponent component, ClockworkItemEnchantEvent args)
    {
        //if (HasComp<EnchantUserComponent>(uid))
    }
}