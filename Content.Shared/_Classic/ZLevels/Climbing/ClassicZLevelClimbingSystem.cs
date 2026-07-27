using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Climbing.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Classic.ZLevels.Climbing;

/// <summary>
/// Allows airborne entities to pass over climbable obstacles (fences, tables) without triggering a climb.
/// </summary>
public sealed class ClassicZLevelClimbingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClassicZPhysicsComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<ClassicZPhysicsComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Disabled)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var physics) || physics.BodyStatus != BodyStatus.InAir)
            return;

        if (HasComp<ClimbableComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
