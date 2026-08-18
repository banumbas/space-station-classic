/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._Classic.ZLevels.Damage.SafeFalling;

public sealed class ClassicSafeFallingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicSafeFallingComponent, ClassicZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }

    private void OnFallingDamageCalculate(Entity<ClassicSafeFallingComponent> ent, ref ClassicZFallingDamageCalculateEvent args)
    {
        if (args.Fallen == ent.Owner)
            return;

        args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
