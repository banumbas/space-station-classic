using System.Numerics;
using System.Linq;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared._Starlight.Antags.Cults.Clockwork;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Anchors;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Physics;
using Content.Server.Station.Components;
using Content.Server.Teleportation;
using Content.Shared.Teleportation.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server._Starlight.StateMachines;
using Content.Server.Administration.Systems;
using Content.Shared._Starlight.StateMachines;
using Content.Shared._Starlight.Abstract;
using Content.Server.SurveillanceCamera;
using static Content.Shared.Pinpointer.SharedNavMapSystem;
using Content.Shared.Pinpointer;
using Content.Server.Chat.Managers;
using Content.Server.Administration.Managers;
using Content.Server.Starlight;
using Content.Shared.Chat;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Robust.Server.Player;

namespace Content.Server._Starlight.Antags.Cult.Clockwork;

public sealed partial class ClockworkPortalSystem : EntitySystem
{
    private readonly HashSet<string> _safeBeacon = ["DefaultStationBeaconArmory", "DefaultStationBeaconVault"];
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly LinkedEntitySystem _linked = default!;
    [Dependency] private readonly EntityStateMachineSystem _esm = default!;
    [Dependency] private readonly StarlightEntitySystem _entity = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClockworkConsoleOpenPortalEvent>(OnOpenPortal);
    }

    private void OnOpenPortal(ClockworkConsoleOpenPortalEvent ev)
    {
        var grid = _transform.GetGrid(ev.Target);
        var targetCoords = _transform.ToMapCoordinates(ev.Target);

        //Temporarily removed for testing.
        //if (grid == null 
        //    || !HasComp<BecomesStationComponent>(grid))
        //    return;

        if (!FindNearestArch(ev.Performer, ev.Spot, out var arch))
            return;

        if(IsAreaRestricted(targetCoords, ev.Performer)) 
            return;

        var reference = EnsureComp<ReferenceComponent>(arch.Value.owner);
        if (reference.Reference is not null
            && TryComp(reference.Reference.Value, out MetaDataComponent? metadata)
            && metadata.LifeStage < ComponentLifeStage.Stopping)
            return;

        var portal1 = Spawn(ev.Portal, arch.Value.coords);
        var portal2 = Spawn(ev.Portal, targetCoords);

        reference.Reference = portal1;

        if (ev.Effect != null)
        {
            SpawnAttachedTo(ev.Effect, Transform(portal1).Coordinates);
            SpawnAttachedTo(ev.Effect, Transform(portal2).Coordinates);
        }

        var @event = new CultPortalLifeTimeEvent();
        RaiseLocalEvent(ref @event);

        if (@event.LifeTime is { } lifeTime)
        {
            if (_entity.TryEntity<EntityStateMachineComponent>(portal1, out var portal1Ent))
                _esm.SetLifeTime(portal1Ent.Value, lifeTime);
            if (_entity.TryEntity<EntityStateMachineComponent>(portal2, out var portal2Ent))
                _esm.SetLifeTime(portal2Ent.Value, lifeTime);
        }

        _linked.TryLink(portal1, portal2);
    }

    private bool IsAreaRestricted(MapCoordinates targetCoords, EntityUid performer)
    {
        var beacons = EntityQueryEnumerator<NavMapBeaconComponent, MetaDataComponent, TransformComponent>();
        while (beacons.MoveNext(out var entity, out var beacon, out var metadata, out var xform))
            if(metadata.EntityPrototype is not null 
                && _safeBeacon.Contains(metadata.EntityPrototype.ID) 
                && xform.MapID == targetCoords.MapId)
            {
                var mapCoords = _transform.GetMapCoordinates(entity, xform);
                if(Vector2.Distance(mapCoords.Position, targetCoords.Position) < 7f)
                {
                    if(_playerManager.TryGetSessionByEntity(performer, out var session))
                    {
                        var message = Loc.GetString("portal-restricted-area");
                        var wrappedMessage = Loc.GetString("portal-restricted-area");
                        _chat.ChatMessageToOne(ChatChannel.Notifications, message, wrappedMessage, default, false, session.Channel, Color.FromHex("#F45858"));
                    }
                    return true;
                }
            }
        return false;
    }

    private bool FindNearestArch(EntityUid performer, EntProtoId archType, [NotNullWhen(true)] out (EntityUid owner, MapCoordinates coords)? arch)
    {
        var performerPos = _transform.GetMapCoordinates(performer);
        LinkedList<(EntityUid owner, MapCoordinates coords)> results = new();
        arch = null;

        var spawners = EntityQueryEnumerator<SpellTargetAnchorComponent, MetaDataComponent, TransformComponent>();

        while (spawners.MoveNext(out var entity, out _, out var metadata, out var xform))
            if (metadata.EntityPrototype?.ID is string proto
                && proto == archType
                && _transform.GetMapCoordinates(entity, xform) is var coords
                && coords.MapId == performerPos.MapId)
                results.AddLast((entity, coords));

        if (results.Count == 1)
        {
            arch = results.First!.Value;
            return true;
        }

        if (results.Count == 0)
            return false;

        var closest = results.First!.Value;
        var dist = (closest.coords.Position - performerPos.Position).LengthSquared();
        foreach (var item in results)
        {
            var newDist = (item.coords.Position - performerPos.Position).LengthSquared();
            if (newDist < dist)
            {
                dist = newDist;
                closest = item;
            }
        }
        arch = closest;
        return true;
    }
}       