using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Stunnable;

namespace Content.Server.Starlight.Antag.Clockwork.Actions;

public sealed partial class StunOnAttackEnchantAction : EnchantAction
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(2f);

    public override void Action(EnchantActionArgs args)
    {
        base.Action(args);
        
        if (entMan == null)
            return;

        var enchanted = entMan.AddComponent<EnchantedComponent>(args.Item);
        
        enchanted.Action = this;
        enchanted.RiseActionOnAttack = true;
    }
    
    public override void Attack(EntityUid User, EntityUid Weapon, IReadOnlyList<EntityUid> HitEntities)
    {
        base.Attack(User, Weapon, HitEntities);
        
        if (entMan == null)
            return;
        
        var _stunSystem = entMan.System<SharedStunSystem>();
        
        foreach (var entity in HitEntities)
        {
            _stunSystem.TryStun(entity, Duration, true);
            _stunSystem.TryKnockdown(entity, Duration, true);
        }
    }
}