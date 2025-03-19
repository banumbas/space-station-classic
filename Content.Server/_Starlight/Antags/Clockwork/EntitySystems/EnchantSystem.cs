using System.Linq;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class EnchantSystem : SharedEnchantSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ClockworkEnchantMessage>(OnItemEnchantMessage);
        SubscribeLocalEvent<EnchantedComponent, MeleeHitEvent>(OnAttack);
        base.Initialize();
    }
    
    private void OnAttack(EntityUid uid, EnchantedComponent component, MeleeHitEvent args)
    {
        if (component.Action != null)
            component.Action.Attack(args.User, args.Weapon, args.HitEntities);
    }
    
    private void OnItemEnchantMessage(ClockworkEnchantMessage ev)
    {
        if (!TryGetEntity(ev.Entity, out var target))
            return;

        var actionArgs = new EnchantActionArgs(target.Value, GetEntity(ev.Item), EntityManager);
        ev.Action.Action(actionArgs);
    }
}