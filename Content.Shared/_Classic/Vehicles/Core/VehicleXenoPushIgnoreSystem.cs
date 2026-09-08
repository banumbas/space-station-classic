using Content.Shared._Classic.Vehicles;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Classic.Vehicles;

public sealed class VehicleXenoPushIgnoreSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleXenoPushIgnoreComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<VehicleXenoPushIgnoreComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<VehicleXenoPushIgnoreComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnPreventCollide(Entity<VehicleXenoPushIgnoreComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<ThrownItemComponent>(ent))
            args.Cancelled = true;
    }

    private void OnLand(Entity<VehicleXenoPushIgnoreComponent> ent, ref LandEvent args)
    {
        if (_net.IsClient)
            return;

        RemCompDeferred<VehicleXenoPushIgnoreComponent>(ent);
    }

    private void OnStopThrow(Entity<VehicleXenoPushIgnoreComponent> ent, ref StopThrowEvent args)
    {
        if (_net.IsClient)
            return;

        RemCompDeferred<VehicleXenoPushIgnoreComponent>(ent);
    }
}
