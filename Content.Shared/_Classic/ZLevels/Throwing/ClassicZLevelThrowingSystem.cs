using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Throwing;

namespace Content.Shared._Classic.ZLevels.Throwing;

/// <summary>
/// Keeps z-physics out of the way of a vanilla throw: while an entity is actually being
/// thrown, its horizontal flight distance/timing is fully governed by ThrowingSystem's
/// friction-based model, so z-physics gravity/ground-sync/BodyStatus-sync must not run
/// for it (that fight is what caused throws to land short or overshoot the cursor).
/// </summary>
public sealed partial class ClassicZLevelThrowingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClassicZPhysicsComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<ClassicZPhysicsComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnThrown(Entity<ClassicZPhysicsComponent> ent, ref ThrownEvent args)
    {
        ent.Comp.Disabled = true;
        DirtyField(ent, ent.Comp, nameof(ClassicZPhysicsComponent.Disabled));
    }

    private void OnStopThrow(Entity<ClassicZPhysicsComponent> ent, ref StopThrowEvent args)
    {
        ent.Comp.Disabled = false;
        DirtyField(ent, ent.Comp, nameof(ClassicZPhysicsComponent.Disabled));
    }
}
