/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Classic.ZLevels.Core;

public sealed partial class ClassicZLevelsSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly EntProtoId _zEyeProto = "ClassicZLevelEye";

    private readonly TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(1f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;

    private void InitView()
    {
        _configuration.OnValueChanged(
            CCVars.ClassicZLevelsRenderingMaxZLevelsBelowRendering,
            OnMaxLevelsBelowChanged);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<ClassicZLevelViewerComponent, MapInitEvent>(OnViewerInit);
        SubscribeLocalEvent<ClassicZLevelViewerComponent, ComponentRemove>(OnCompRemove);

        SubscribeLocalEvent<ClassicZLevelViewerComponent, MapUidChangedEvent>(OnViewerMapUidChanged);
    }

    private void ShutdownView()
    {
        _configuration.UnsubValueChanged(
            CCVars.ClassicZLevelsRenderingMaxZLevelsBelowRendering,
            OnMaxLevelsBelowChanged);
    }

    private void OnMaxLevelsBelowChanged(int value)
    {
        var query = EntityQueryEnumerator<ClassicZLevelViewerComponent>();
        while (query.MoveNext(out var uid, out var viewer))
            UpdateViewer((uid, viewer));
    }

    private void UpdateView(float frameTime)
    {
        if (_timing.CurTime < _nextZLevelViewerUpdate)
            return;
        _nextZLevelViewerUpdate = _timing.CurTime + _zLevelViewerUpdateRate;

        var query = EntityQueryEnumerator<ClassicZLevelViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            foreach (var eye in viewer.Eyes)
            {
                _transform.SetWorldPosition(eye, _transform.GetWorldPosition(xform));
            }
        }
    }

    private void OnViewerInit(Entity<ClassicZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionId);
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnCompRemove(Entity<ClassicZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var viewer = EnsureComp<ClassicZLevelViewerComponent>(ev.Entity);
        UpdateViewer((ev.Entity, viewer));
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        RemComp<ClassicZLevelViewerComponent>(ev.Entity);
    }

    private void OnViewerMapUidChanged(Entity<ClassicZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        UpdateViewer(ent);
    }

    private void UpdateViewer(Entity<ClassicZLevelViewerComponent> ent)
    {
        var eyes = ent.Comp.Eyes;
        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }
        eyes.Clear();

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var xform = Transform(ent);
        var map = xform.MapUid;

        if (map is null)
            return;

        var globalPos = _transform.GetWorldPosition(xform);

        // Match the replicated render limit, while always preloading one physical landing level.
        // A render limit of zero must not leave procedural stone absent until after a player falls
        // into that map. With the default value, only the one adjacent lower level is subscribed.
        var maxLevelsBelow = Math.Clamp(
            Math.Max(1, _configuration.GetCVar(CCVars.ClassicZLevelsRenderingMaxZLevelsBelowRendering)),
            1,
            MaxZLevelsBelowRendering);

        for (var i = 1; i <= maxLevelsBelow; i++)
        {
            if (!TryMapOffset(map.Value, -i, out var mapUidBelow))
                break;

            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(mapUidBelow, globalPos));

            Transform(newEye).GridTraversal = false;
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
            eyes.Add(newEye);
        }

        // Keep only the adjacent upper level warm. On -Z3, subscribing all three possible upper
        // maps would stream -Z2, -Z1 and the surface even though normal rendering/climbing only
        // needs the next map. This bounds biome, PVS and network work independently of depth.
        const int upperPreloadLevels = 1;
        for (var i = 1; i <= Math.Min(upperPreloadLevels, MaxZLevelsAboveRendering); i++)
        {
            if (!TryMapOffset(map.Value, i, out var mapUidAbove))
                break;

            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(mapUidAbove, globalPos));

            Transform(newEye).GridTraversal = false;
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
            eyes.Add(newEye);
        }
    }
}
