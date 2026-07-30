using Content.Shared._Classic.PMC;
using Content.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Server._Classic.PMC;

public sealed class OrbitalDesignatorSystem : SharedOrbitalDesignatorSystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrbitalDesignatorComponent, OrbitalDesignatorDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(EntityUid uid, OrbitalDesignatorComponent component, OrbitalDesignatorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (TryComp<UseDelayComponent>(uid, out var useDelay))
        {
            if (_useDelay.IsDelayed((uid, useDelay)))
                return;

            _useDelay.TryResetDelay((uid, useDelay));
        }

        var coords = GetCoordinates(args.TargetPosition);
        if (coords.IsValid(EntityManager))
        {
            Spawn(component.MarkerPrototype, coords);
        }

        args.Handled = true;
    }
}
