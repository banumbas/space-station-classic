using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Classic.Salvage.Fulton;

public abstract partial class SharedClassicCargoFultonSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public static readonly EntProtoId EffectProto = "FultonEffect";
    protected static readonly Vector2 EffectOffset = Vector2.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicCargoFultonComponent, AfterInteractEvent>(OnCargoFultonInteract);
        SubscribeLocalEvent<ClassicCargoFultonDoAfterEvent>(OnCargoFultonDoAfter);

        SubscribeLocalEvent<ClassicFultonSoldComponent, GetVerbsEvent<InteractionVerb>>(OnFultonedGetVerbs);
        SubscribeLocalEvent<ClassicFultonSoldComponent, ExaminedEvent>(OnFultonedExamine);
        SubscribeLocalEvent<ClassicFultonSoldComponent, EntGotInsertedIntoContainerMessage>(OnFultonContainerInserted);
    }

    private void OnFultonContainerInserted(
        EntityUid uid,
        ClassicFultonSoldComponent component,
        EntGotInsertedIntoContainerMessage args)
    {
        RemCompDeferred<ClassicFultonSoldComponent>(uid);
    }

    private void OnFultonedExamine(EntityUid uid, ClassicFultonSoldComponent component, ExaminedEvent args)
    {
        var remaining = component.NextFulton + _metadata.GetPauseTime(uid) - Timing.CurTime;
        var message = Loc.GetString("fulton-examine", ("time", $"{remaining.TotalSeconds:0.00}"));

        args.PushText(message);
    }

    private void OnFultonedGetVerbs(EntityUid uid, ClassicFultonSoldComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new InteractionVerb()
        {
            Text = Loc.GetString("fulton-remove"),
            Act = () => Unfulton(uid, component)
        });
    }

    private void Unfulton(EntityUid uid, ClassicFultonSoldComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || !component.Removeable)
            return;

        RemCompDeferred<ClassicFultonSoldComponent>(uid);
    }

    private void OnCargoFultonDoAfter(ClassicCargoFultonDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target == null ||
            args.Used == null ||
            !TryComp<ClassicCargoFultonComponent>(args.Used.Value, out var fulton))
        {
            return;
        }

        if (!CanUseCargoFulton(args.Target.Value, fulton) ||
            !CanApplyCargoFulton(args.Target.Value, fulton))
        {
            return;
        }

        if (!CanCompleteCargoFulton(args.Used.Value, args.Target.Value, args.User, fulton))
            return;

        if (!_stack.TryUse(args.Used.Value, 1))
            return;

        var fultoned = EnsureComp<ClassicFultonSoldComponent>(args.Target.Value);
        fultoned.NextFulton = Timing.CurTime + fulton.FultonDuration;
        fultoned.FultonDuration = fulton.FultonDuration;
        fultoned.Sound = fulton.LaunchSound;
        fultoned.Removeable = fulton.Removeable;
        UpdateAppearance(args.Target.Value, fultoned);
        Dirty(args.Target.Value, fultoned);

        OnCargoFultonApplied(args.Used.Value, args.Target.Value, args.User, fulton, fultoned);
        Audio.PlayPredicted(fulton.FultonSound, args.Target.Value, args.User);
    }

    private void OnCargoFultonInteract(EntityUid uid, ClassicCargoFultonComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || args.Handled || !args.CanReach)
            return;

        if (!CanUseCargoFulton(args.Target.Value, component))
        {
            _popup.PopupClient(Loc.GetString("fulton-invalid"), uid, args.User);
            return;
        }

        if (HasComp<ClassicFultonSoldComponent>(args.Target.Value))
        {
            _popup.PopupClient(Loc.GetString("fulton-fultoned"), uid, args.User);
            return;
        }

        if (!CanApplyCargoFulton(args.Target.Value, component))
        {
            _popup.PopupClient(Loc.GetString("fulton-invalid"), uid, args.User);
            return;
        }

        args.Handled = true;

        var ev = new ClassicCargoFultonDoAfterEvent();
        _doAfter.TryStartDoAfter(
            new DoAfterArgs(EntityManager, args.User, component.ApplyFultonDuration, ev, args.Target.Value, args.Target.Value, uid)
            {
                MovementThreshold = 0.5f,
                BreakOnMove = true,
                Broadcast = true,
                NeedHand = true,
            });
    }

    protected virtual bool CanCompleteCargoFulton(
        EntityUid fultonUid,
        EntityUid targetUid,
        EntityUid userUid,
        ClassicCargoFultonComponent component)
    {
        return true;
    }

    protected virtual void OnCargoFultonApplied(
        EntityUid fultonUid,
        EntityUid targetUid,
        EntityUid userUid,
        ClassicCargoFultonComponent fulton,
        ClassicFultonSoldComponent fultoned)
    {
    }

    protected virtual void UpdateAppearance(EntityUid uid, ClassicFultonSoldComponent fultoned)
    {
    }

    protected bool CanUseCargoFulton(EntityUid target, ClassicCargoFultonComponent component)
    {
        return !component.RequiresSensorTower || TryFindActiveSensorTower(target, out _);
    }

    protected bool CanApplyCargoFulton(EntityUid targetUid, ClassicCargoFultonComponent component)
    {
        if (!CanCargoFulton(targetUid))
            return false;

        if (_whitelistSystem.IsWhitelistFailOrNull(component.Whitelist, targetUid))
            return false;

        return true;
    }

    protected bool CanCargoFulton(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.Anchored)
            return false;

        if (Container.IsEntityInContainer(uid))
            return false;

        return true;
    }

    public bool TryFindActiveSensorTower(EntityUid targetUid, out EntityUid towerUid)
    {
        towerUid = EntityUid.Invalid;

        if (!TryComp<TransformComponent>(targetUid, out var targetXform))
            return false;

        var targetCoords = TransformSystem.GetMapCoordinates(targetUid, xform: targetXform);
        var query = EntityQueryEnumerator<FultonSensorTowerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tower, out var towerXform))
        {
            if (!IsSensorTowerPowered(uid))
                continue;

            var towerCoords = TransformSystem.GetMapCoordinates(uid, xform: towerXform);
            if (towerCoords.MapId != targetCoords.MapId)
                continue;

            if ((towerCoords.Position - targetCoords.Position).LengthSquared() > tower.Range * tower.Range)
                continue;

            towerUid = uid;
            return true;
        }

        return false;
    }

    private bool IsSensorTowerPowered(EntityUid uid)
    {
        return TryComp<AppearanceComponent>(uid, out var appearance) &&
               _appearance.TryGetData<bool>(uid, PowerDeviceVisuals.Powered, out var powered, appearance) &&
               powered;
    }

    [Serializable, NetSerializable]
    private sealed partial class ClassicCargoFultonDoAfterEvent : SimpleDoAfterEvent
    {
    }

    [Serializable, NetSerializable]
    protected sealed class ClassicFultonAnimationMessage : EntityEventArgs
    {
        public NetEntity Entity;
        public NetCoordinates Coordinates;
    }
}
