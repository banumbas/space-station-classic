using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Classic.PMC;

public abstract class SharedOrbitalDesignatorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrbitalDesignatorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, OrbitalDesignatorComponent component, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (component.DoAfter != null && _doAfter.IsRunning(component.DoAfter.Value))
            return;

        if (TryComp<UseDelayComponent>(uid, out var useDelay) && _useDelay.IsDelayed((uid, useDelay)))
            return;

        if (TryComp<WieldableComponent>(uid, out var wieldable) && !wieldable.Wielded)
            return;

        var user = args.User;
        var userCoords = _transform.GetMapCoordinates(user);
        var targetCoords = _transform.ToMapCoordinates(args.ClickLocation);

        if (userCoords.MapId != targetCoords.MapId || userCoords.MapId == MapId.Nullspace)
            return;

        if (!_examine.InRangeUnOccluded(userCoords, targetCoords, component.Range, targetUid => targetUid == user || targetUid == uid))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new OrbitalDesignatorDoAfterEvent(GetNetCoordinates(args.ClickLocation)), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = component.Range
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
        {
            component.DoAfter = doAfterId;
            component.TargetCoordinates = args.ClickLocation;
            component.TargetUser = user;
            args.Handled = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<OrbitalDesignatorComponent>();
        while (query.MoveNext(out var rangefinderUid, out var rangefinderComp))
        {
            if (rangefinderComp.DoAfter is not { } doAfterId)
                continue;

            if (!_doAfter.IsRunning(doAfterId))
            {
                rangefinderComp.DoAfter = null;
                rangefinderComp.TargetUser = null;
                continue;
            }

            if (rangefinderComp.TargetUser is not { } user || !Exists(user))
            {
                _doAfter.Cancel(doAfterId);
                rangefinderComp.DoAfter = null;
                rangefinderComp.TargetUser = null;
                continue;
            }

            if (TryComp<WieldableComponent>(rangefinderUid, out var wieldable) && !wieldable.Wielded)
            {
                _doAfter.Cancel(doAfterId);
                rangefinderComp.DoAfter = null;
                rangefinderComp.TargetUser = null;
                continue;
            }

            var coordinates = rangefinderComp.TargetCoordinates;
            if (!coordinates.IsValid(EntityManager))
            {
                _doAfter.Cancel(doAfterId);
                rangefinderComp.DoAfter = null;
                rangefinderComp.TargetUser = null;
                continue;
            }

            var userCoords = _transform.GetMapCoordinates(user);
            var targetCoords = _transform.ToMapCoordinates(coordinates);

            // Anti-cheat check: Range check protects against exploit packets attempting to designate targets beyond allowed range.
            // Line-of-sight check: Continuously cancels DoAfter if target position becomes occluded by a wall or obstacle.
            if (userCoords.MapId != targetCoords.MapId ||
                !_examine.InRangeUnOccluded(userCoords, targetCoords, rangefinderComp.Range, uid => uid == user || uid == rangefinderUid))
            {
                _doAfter.Cancel(doAfterId);
                rangefinderComp.DoAfter = null;
                rangefinderComp.TargetUser = null;
            }
        }
    }
}


