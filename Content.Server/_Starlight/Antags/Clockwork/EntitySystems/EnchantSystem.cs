using System.Linq;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class EnchantSystem : SharedEnchantSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantedComponent, MeleeHitEvent>(OnAttack);
        Subs.BuiEvents<EnchantUserComponent>(EnchantUIKey.Key, subs =>
        {
            subs.Event<ClockworkEnchantMessage>(OnItemEnchantMessage);
        });
        base.Initialize();
    }
    
    private void OnAttack(EntityUid uid, EnchantedComponent component, MeleeHitEvent args)
    {
        if (component.Action != null)
            component.Action.Attack(args.User, args.Weapon, args.HitEntities);
    }
    
    private void OnItemEnchantMessage(EntityUid uid, EnchantUserComponent component, ClockworkEnchantMessage ev)
    {
        Logger.Warning("EnchantSystem: Trying to start enchant action");
        
        var actionArgs = new EnchantActionArgs(uid, GetEntity(ev.Item), EntityManager);
        ev.Action.Action(actionArgs);
    }
}