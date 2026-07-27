/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Damage.Systems;

namespace Content.Shared._Classic.ZLevels.Damage.FallingDamage;

public sealed partial class ClassicFallingDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicFallingDamageComponent, ClassicZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<ClassicFallingDamageComponent> ent, ref ClassicZFellOnMeEvent args)
    {
        _damageable.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
