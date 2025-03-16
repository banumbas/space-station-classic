using Content.Server.Actions;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Content.Shared._Starlight.Computers.RemoteEye;
using System.Linq;
using Content.Shared.Actions;

namespace Content.Server._Starlight.Computers.RemoteEye;

public sealed partial class RemoteEyeSystem : SharedRemoteEyeSystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RemoteEyeConsoleComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttemptEvent);
        SubscribeLocalEvent<RemoteEyeConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<ExitConsoleEvent>(OnExit);

        Subs.BuiEvents<RemoteEyeConsoleComponent>(RemoteEyeUIKey.Key, subs => subs.Event<BeaconChosenBuiMsg>(OnBeaconChosenBuiMsg));
        base.Initialize();
    }

    private void OnActivatableUIOpenAttemptEvent(Entity<RemoteEyeConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        foreach (var reg in (ent.Comp.RequiredComponents ?? []).Values)
            if (!HasComp(args.User, reg.Component.GetType()))
                args.Cancel();
    }

    private void OnBeforeActivatableUIOpen(Entity<RemoteEyeConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        var stations = _stationSystem.GetStations();
        var result = new Dictionary<int, StationBeacons>();

        foreach (var station in stations)
        {
            if (_stationSystem.GetLargestGrid(Comp<StationDataComponent>(station)) is not { } grid
                || !TryComp(station, out MetaDataComponent? stationMetaData))
                continue;

            if (!_entityManager.TryGetComponent<NavMapComponent>(grid, out var navMap))
                continue;

            result.Add(station.Id, new StationBeacons
            {
                Name = stationMetaData.EntityName,
                StationId = station.Id,
                Beacons = [.. navMap.Beacons.Values],
            });
        }

        _uiSystem.SetUiState(ent.Owner, RemoteEyeUIKey.Key, new RemoteEyeConsoleBuiState() { Stations = result, Color = ent.Comp.Color });
    }
    private void OnBeaconChosenBuiMsg(Entity<RemoteEyeConsoleComponent> ent, ref BeaconChosenBuiMsg args)
    {
        CameraExit(args.Actor);
        var beacon = _entityManager.GetEntity(args.Beacon.NetEnt);
        var eye = SpawnAtPosition(ent.Comp.RemoteEntityProto, Transform(beacon).Coordinates);
        ent.Comp.RemoteEntity = GetNetEntity(eye);

        if (TryComp<HandsComponent>(args.Actor, out var handsComponent))
            foreach (var hand in _hands.EnumerateHands(args.Actor, handsComponent))
            {

                if (hand.HeldEntity == null)
                {
                    if (HasComp<UnremoveableComponent>(hand.HeldEntity))
                        continue;

                    _hands.DoDrop(args.Actor, hand, true, handsComponent);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Actor, out var virtItem))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }

        if (TryComp(args.Actor, out EyeComponent? eyeComp))
        {
            _eye.SetVisibilityMask(args.Actor, eyeComp.VisibilityMask | (int)VisibilityFlags.Net, eyeComp);
            _eye.SetTarget(args.Actor, eye, eyeComp);
            _eye.SetDrawFov(args.Actor, false);

            if (TryComp<StationAiOverlayComponent>(eye, out var eyeOverlay))
                AddComp(args.Actor, new StationAiOverlayComponent
                {
                    AllowCrossGrid = eyeOverlay.AllowCrossGrid,
                    Alfa = eyeOverlay.Alfa
                });

            if (!TryComp(eye, out RemoteEyeSourceContainerComponent? remoteEyeSourceContainerComponent))
            {
                remoteEyeSourceContainerComponent = new RemoteEyeSourceContainerComponent { Actor = args.Actor };
                AddComp(eye, remoteEyeSourceContainerComponent);
            }
            else
                remoteEyeSourceContainerComponent.Actor = args.Actor;

            Dirty(eye, remoteEyeSourceContainerComponent);
        }

        var remoteEyeActor = EnsureComp<RemoteEyeActorComponent>(args.Actor);
        remoteEyeActor.VirtualItem = ent.Owner;

        AddActions(ent, (args.Actor, remoteEyeActor));

        Dirty(ent);
        _mover.SetRelay(args.Actor, eye);
    }
    private void OnExit(ExitConsoleEvent ev) => CameraExit(ev.Performer);

    public void CameraExit(EntityUid actor)
    {
        if (!TryComp<RelayInputMoverComponent>(actor, out var comp)) return;

        RemoveActions(actor, out var remoteEyeActor);

        if (remoteEyeActor.VirtualItem.HasValue)
            _virtualItem.DeleteInHandsMatching(actor, remoteEyeActor.VirtualItem.Value);

        if (TryComp(actor, out EyeComponent? eyeComp))
        {
            if (HasComp<StationAiOverlayComponent>(actor))
                RemComp<StationAiOverlayComponent>(actor);

            _eye.SetVisibilityMask(actor, eyeComp.VisibilityMask ^ (int)VisibilityFlags.Net, eyeComp);
            _eye.SetDrawFov(actor, true);
        }

        RemComp(actor, comp);
        QueueDel(comp.RelayEntity);
    }
    private void AddActions(Entity<RemoteEyeConsoleComponent> ent, Entity<RemoteEyeActorComponent> performer)
    {
        performer.Comp.HiddenActions = _actions.HideActions(performer);

        performer.Comp.ActionsEntities = new EntityUid?[ent.Comp.Actions.Length];
        for (var i = 0; i < ent.Comp.Actions.Length; i++)
            _actions.AddAction(performer, ref performer.Comp.ActionsEntities[i], ent.Comp.Actions[i]);
    }
    private void RemoveActions(EntityUid actor, out RemoteEyeActorComponent comp)
    {
        EnsureComp(actor, out comp);

        foreach (var actionEnt in comp.ActionsEntities.Where(x => x is not null))
        {
            _actionContainer.RemoveAction(actionEnt!.Value); //For real, what the hell — why isn’t it cleaning itself up?
            _actions.RemoveAction(actor, actionEnt);
        }

        _actions.UnHideActions(actor, comp.HiddenActions);
    }
}
