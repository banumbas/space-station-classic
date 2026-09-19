/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Server.Parallax;
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
    [Dependency] private TagSystem _viewerTags = default!;

    private readonly EntProtoId _zEyeProto = "ClassicZLevelEye";
    private static readonly ProtoId<TagPrototype> AllowBiomeLoadingTag = "AllowBiomeLoading";

    private readonly TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(1f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;

    private void InitView()
    {
        _configuration.OnValueChanged(
            CCVars.ClassicZLevelsRenderingMaxZLevelsBelowRendering,
            OnMaxLevelsBelowChanged);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<GhostComponent, ComponentInit>(OnGhostStateChanged);

        SubscribeLocalEvent<ClassicZLevelViewerComponent, MapInitEvent>(OnViewerInit);
        SubscribeLocalEvent<ClassicZLevelViewerComponent, ComponentRemove>(OnCompRemove);

        SubscribeLocalEvent<ClassicZLevelViewerComponent, ClassicZLevelBeforeMapMoveEvent>(OnViewerBeforeMapMove);
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

    /// <summary>Whether an entity is an auxiliary Z-eye belonging to this viewer.</summary>
    public bool IsViewerEye(EntityUid? viewer, EntityUid eye)
    {
        return TryComp<ClassicZLevelViewerComponent>(viewer, out var component) && component.Eyes.Contains(eye);
    }

    private void OnCompRemove(Entity<ClassicZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        TryComp<ActorComponent>(ent, out var actor);
        foreach (var eye in ent.Comp.Eyes)
        {
            CacheEyeBiome(eye);
            if (actor != null)
                _viewSubscriber.RemoveViewSubscriber(eye, actor.PlayerSession);
            QueueDel(eye);
        }
        ent.Comp.Eyes.Clear();
        ent.Comp.UpperEyes.Clear();
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

    private void OnGhostStateChanged(Entity<GhostComponent> ent, ref ComponentInit args)
    {
        if (TryComp<ClassicZLevelViewerComponent>(ent, out var viewer))
            UpdateViewer((ent.Owner, viewer));
    }

    private void OnViewerMapUidChanged(Entity<ClassicZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        UpdateViewer(ent);
    }

    private void OnViewerBeforeMapMove(
        Entity<ClassicZLevelViewerComponent> ent,
        ref ClassicZLevelBeforeMapMoveEvent args)
    {
        CacheEyeBiome(ent);
    }

    protected override void LookUpChanged(Entity<ClassicZLevelViewerComponent> entity)
    {
        UpdateUpperEyes(entity);
    }

    private void UpdateViewer(Entity<ClassicZLevelViewerComponent> ent)
    {
        var eyes = ent.Comp.Eyes;
        TryComp<ActorComponent>(ent, out var actor);
        foreach (var eye in ent.Comp.Eyes)
        {
            CacheEyeBiome(eye);
            if (actor != null)
                _viewSubscriber.RemoveViewSubscriber(eye, actor.PlayerSession);
            QueueDel(eye);
        }
        eyes.Clear();
        ent.Comp.UpperEyes.Clear();

        if (HasComp<GhostComponent>(ent) && !_viewerTags.HasTag(ent, AllowBiomeLoadingTag))
            return;

        if (actor == null)
            return;

        var xform = Transform(ent);
        var map = xform.MapUid;

        if (map is null)
            return;

        var globalPos = _transform.GetWorldPosition(xform);

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

        UpdateUpperEyes(ent);
    }

    private void UpdateUpperEyes(Entity<ClassicZLevelViewerComponent> ent)
    {
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        foreach (var eye in ent.Comp.UpperEyes)
        {
            CacheEyeBiome(eye);
            _viewSubscriber.RemoveViewSubscriber(eye, actor.PlayerSession);
            ent.Comp.Eyes.Remove(eye);
            QueueDel(eye);
        }
        ent.Comp.UpperEyes.Clear();

        if (!ent.Comp.LookUp ||
            HasComp<GhostComponent>(ent) && !_viewerTags.HasTag(ent, AllowBiomeLoadingTag))
        {
            return;
        }

        var xform = Transform(ent);
        if (xform.MapUid is not { } map || !TryMapOffset(map, 1, out var mapAbove))
            return;

        var globalPos = _transform.GetWorldPosition(xform);
        var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(mapAbove, globalPos));
        Transform(newEye).GridTraversal = false;
        _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
        ent.Comp.Eyes.Add(newEye);
        ent.Comp.UpperEyes.Add(newEye);
    }

    private void CacheEyeBiome(EntityUid eye)
    {
        EntityManager.System<BiomeSystem>().CacheClassicZLevel(eye);
    }
}
