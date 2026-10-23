using Content.Shared._Classic.Vehicles.Viewport;
using Content.Shared.Movement.Events;
using Robust.Shared.GameObjects;

namespace Content.Client._Classic.Vehicles.Viewport;

public sealed class VehicleViewportSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleViewportUserComponent, MoveInputEvent>(OnUserMove);
    }

    private void OnUserMove(Entity<VehicleViewportUserComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (TryComp(ent, out EyeComponent? eye))
            _eye.SetTarget(ent.Owner, ent.Comp.PreviousTarget, eye);
    }
}
