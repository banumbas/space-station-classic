using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Eye.Blinding.Components;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Tools.Components;
using Content.Shared.VentCrawl.Components;
using Content.Shared.VentCrawl.Tube.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.VentCrawl.EntitySystems;

/// <summary>
/// A system that handles the crawling behavior for vent creatures.
/// </summary>
public sealed partial class SharedVentCrawlableSystem : EntitySystem
{
    [Dependency] private SharedVentCrawlTubeSystem _ventCrawlTubeSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private BlindableSystem _blindable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlHolderComponent, MoveInputEvent>(OnMoveInput);

        SubscribeLocalEvent<BeingVentCrawlComponent, ExitVentActionEvent>(OnExitVentActionEvent);
    }

    /// <summary>
    /// Handles the MoveInputEvent for <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    private void OnMoveInput(EntityUid uid, VentCrawlHolderComponent holder, ref MoveInputEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (!Exists(holder.CurrentTube))
        {
            ExitVentCrawl(uid);
            return;
        }

        var dir = args.Dir;
        if (dir != Direction.Invalid && TryComp<InputMoverComponent>(holder.ContainedEntity, out var mover))
        {
            var cameraAngle = mover.TargetRelativeRotation;
            if (cameraAngle != Angle.Zero)
                dir = (dir.ToAngle() + cameraAngle).GetCardinalDir();
        }

        holder.IsMoving = args.HasDirectionalMovement;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.IsMoving));

        if (holder.CurrentDirection != dir && dir != Direction.Invalid)
        {
            holder.PreviousDirection = holder.CurrentDirection;
            holder.CurrentDirection = dir;

            DirtyField(uid, holder, nameof(VentCrawlHolderComponent.CurrentDirection));
            DirtyField(uid, holder, nameof(VentCrawlHolderComponent.PreviousDirection));
        }
    }

    /// <summary>
    /// Tries to insert an entity into the <seealso cref="VentCrawlHolderComponent"/> container.
    /// </summary>
    /// <returns>True if the insertion was successful, otherwise False.</returns>
    public bool TryInsert(EntityUid uid, EntityUid toInsert)
    {
        if (!CanInsert(uid, toInsert))
            return false;

        if (!_containerSystem.Insert(toInsert, GetOrEnsureContainer(uid)))
            return false;

        if (TryComp<PhysicsComponent>(toInsert, out var physBody))
            _physicsSystem.SetCanCollide(toInsert, false, body: physBody);

        return true;
    }

    /// <summary>
    /// Checks whether the specified entity can be inserted into the container of the <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    /// <returns>True if the entity can be inserted into the container; otherwise, False.</returns>
    private bool CanInsert(EntityUid uid, EntityUid toInsert)
        => _containerSystem.CanInsert(toInsert, GetOrEnsureContainer(uid)) && (HasComp<ItemComponent>(toInsert) || HasComp<BodyComponent>(toInsert));

    private Container GetOrEnsureContainer(EntityUid uid)
        => _containerSystem.EnsureContainer<Container>(uid, nameof(VentCrawlHolderComponent));

    /// <summary>
    /// Attempts to make the <seealso cref="VentCrawlHolderComponent"/> enter a <seealso cref="VentCrawlTubeComponent"/>.
    /// </summary>
    /// <returns>True if the <seealso cref="VentCrawlHolderComponent"/> successfully enters the <seealso cref="VentCrawlTubeComponent"/>; otherwise, False.</returns>
    public bool EnterTube(EntityUid holderUid, EntityUid toUid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null, VentCrawlTubeComponent? to = null, TransformComponent? toTransform = null)
    {

        if (!Exists(holderUid))
            return false;

        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;

        if (holder.IsExitingVentCrawls)
            return false;

        if (!Resolve(toUid, ref to, ref toTransform, false))
            return false;

        if (!_containerSystem.Insert(holderUid, _ventCrawlTubeSystem.GetOrEnsureContainer(toUid, to)))
        {
            Log.Error("Entity tried entering tube but container system can't insert it into tube!");
            return false;
        }

        var player = holder.ContainedEntity;

        var beingcrawl = EnsureComp<BeingVentCrawlComponent>(player);
        beingcrawl.Holder = holderUid;
        Dirty(player, beingcrawl);

        if (HasComp<ParentCanBlockVisionComponent>(player))
            _blindable.UpdateIsBlind(player, true);

        var welded = false;
        if (TryComp<WeldableComponent>(toUid, out var weldableComponent))
            welded = weldableComponent.IsWelded;

        var isValidExit = HasComp<VentCrawlEntryComponent>(toUid) && !welded;

        if (isValidExit && !holder.HasExitAction)
        {
            var action = _actionsSystem.AddAction(player, holder.ActionProto);
            if (action != null)
                holder.ProvidedActions.Add(action.Value);

            holder.HasExitAction = true;
        }
        else if (!isValidExit && holder.HasExitAction)
        {
            foreach (var action in holder.ProvidedActions)
                _actionsSystem.RemoveAction(action);

            holder.ProvidedActions.Clear();
            holder.HasExitAction = false;
        }

        if (TryComp<PhysicsComponent>(holderUid, out var physBody))
            _physicsSystem.SetCanCollide(holderUid, false, body: physBody);

        if (holder.CurrentTube != null && TryComp<AtmosPipeLayersComponent>(toUid, out var toLayers))
        {
            var offset = GetLayerOffset(toLayers.CurrentPipeLayer);
            var tubePos = Transform(toUid).Coordinates;
            _xformSystem.SetCoordinates(holderUid, _xformSystem.WithEntityId(tubePos.Offset(offset), toUid));
        }

        if (HasComp<VentCrawlManifoldComponent>(toUid))
        {
            holder.ManifoldLayer = holder.PreviousTube != null && TryComp<AtmosPipeLayersComponent>(holder.PreviousTube, out var prevLayers)
                ? TransformIntoManifoldLayer(prevLayers.CurrentPipeLayer)
                : 0;

            holder.PreviousManifoldLayer = null;
            holder.ManifoldTransitionStart = TimeSpan.Zero;
            holder.ManifoldTransitionEnd = TimeSpan.Zero;

            UpdateManifoldPosition(toUid, holderUid, holder);
        }
        else
            holder.ManifoldLayer = null;

        holder.PreviousTube = holder.CurrentTube;
        holder.PreviousDirection = holder.CurrentDirection;
        holder.CurrentTube = toUid;
        Dirty(holderUid, holder);

        return true;
    }

    private void OnExitVentActionEvent(EntityUid uid, BeingVentCrawlComponent component, ExitVentActionEvent args)
        => ExitVentCrawl(component.Holder);

    /// <summary>
    /// Exits the vent craws for the specified <seealso cref="VentCrawlHolderComponent"/>, removing it and any contained entities from the craws.
    /// </summary>
    public void ExitVentCrawl(EntityUid uid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (Terminating(uid) || Deleted(uid))
            return;

        if (!Resolve(uid, ref holder, ref holderTransform, false))
            return;

        if (holder.IsExitingVentCrawls)
            return;

        holder.IsExitingVentCrawls = true;

        if (holder.HasExitAction)
        {
            foreach (var action in holder.ProvidedActions)
                _actionsSystem.RemoveAction(action);

            holder.ProvidedActions.Clear();
            holder.HasExitAction = false;
        }

        var container = GetOrEnsureContainer(uid);

        foreach (var entity in container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawlComponent>(entity);

            _containerSystem.Remove(entity, container, reparent: false, force: true);

            var xform = Transform(entity);
            if (xform.ParentUid != uid)
                continue;

            _xformSystem.AttachToGridOrMap(entity, xform);

            if (TryComp<VentCrawlerComponent>(entity, out var ventCrawComp))
            {
                ventCrawComp.InTube = false;
                Dirty(entity, ventCrawComp);
            }

            if (TryComp<PhysicsComponent>(entity, out var physics))
                _physicsSystem.WakeBody(entity, body: physics);
        }

        PredictedQueueDel(uid);
    }

    /// <summary>
    /// Updates entities with <seealso cref="VentCrawlHolderComponent"/> and processes their movement in vents.
    /// </summary>
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VentCrawlHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (holder.CurrentTube == null)
                continue;

            if (holder.IsExitingVentCrawls)
                continue;

            if (_gameTiming.CurTime < holder.ManifoldTransitionEnd)
                UpdateManifoldPositionInterpolated(holder.CurrentTube.Value, uid, holder);

            if (!_gameTiming.IsFirstTimePredicted)
                continue;

            if (holder.NextTube != null && _gameTiming.CurTime < holder.MoveEndTime)
                UpdatePosition(holder.CurrentTube.Value, uid, holder);

            if (!UpdateMovementInput(holder.CurrentTube.Value, uid, holder))
                continue;

            if (holder.NextTube != null && _gameTiming.CurTime >= holder.MoveEndTime)
                TryAdvanceTube(holder.CurrentTube.Value, uid, holder);
        }
    }

    private bool UpdateMovementInput(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (!holder.IsMoving)
        {
            holder.CurrentDirection = Direction.Invalid;
            return true;
        }

        if (holder.CurrentDirection == Direction.Invalid)
            return true;

        if (TryComp<VentCrawlManifoldComponent>(currentTube, out var manifold))
            return UpdateManifoldInput(currentTube, uid, holder, manifold);

        if (holder.NextTube == null || holder.NextTube == holder.CurrentTube)
        {
            var nextTube = _ventCrawlTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection);

            if (nextTube != null)
            {
                if (!Exists(holder.CurrentTube))
                {
                    ExitVentCrawl(uid);
                    return false;
                }

                if (nextTube == holder.CurrentTube)
                    return false;

                holder.NextTube = nextTube;
                holder.MoveStartTime = _gameTiming.CurTime;
                holder.MoveEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(holder.TravelDuration);
                Dirty(uid, holder);
            }
            else
            {
                var ev = new GetVentCrawlsConnectableDirectionsEvent();
                RaiseLocalEvent(currentTube, ref ev);
                if (ev.Connectable.Contains(holder.CurrentDirection))
                {
                    ExitVentCrawl(uid);
                    return false;
                }
            }
        }

        return true;
    }

    private void UpdatePosition(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (holder.NextTube == null)
            return;

        if (holder.CurrentDirection == Direction.Invalid)
            return;

        var totalSeconds = (holder.MoveEndTime - holder.MoveStartTime).TotalSeconds;
        if (totalSeconds <= 0)
            return;

        var elapsed = (_gameTiming.CurTime - holder.MoveStartTime).TotalSeconds;
        var progress = (float)Math.Clamp(elapsed / totalSeconds, 0.0, 1.0);

        var origin = Transform(currentTube).Coordinates;

        var nextCoords = Transform(holder.NextTube.Value).Coordinates;
        var delta = nextCoords.Position - origin.Position;

        var newPosition = delta * progress;

        if (TryComp<AtmosPipeLayersComponent>(currentTube, out var layersComp))
        {
            var currentOffset = GetLayerOffset(layersComp.CurrentPipeLayer);
            var nextOffset = TryComp<AtmosPipeLayersComponent>(holder.NextTube.Value, out var nextTubeComp)
                ? GetLayerOffset(nextTubeComp.CurrentPipeLayer)
                : currentOffset;

            var layerOffset = Vector2.Lerp(currentOffset, nextOffset, progress);
            newPosition += layerOffset;
        }

        _xformSystem.SetCoordinates(uid, _xformSystem.WithEntityId(origin.Offset(newPosition), currentTube));
    }

    private void TryAdvanceTube(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (holder.NextTube == null || holder.NextTube == holder.CurrentTube)
        {
            holder.NextTube = null;
            holder.MoveStartTime = TimeSpan.Zero;
            holder.MoveEndTime = TimeSpan.Zero;
            return;
        }

        if (TryComp<VentCrawlTubeComponent>(currentTube, out var tubeComp)
            && _ventCrawlTubeSystem.GetOrEnsureContainer(currentTube, tubeComp) is { } tubeContainer
            && tubeContainer.ContainedEntities.Contains(uid))
            _containerSystem.Remove(uid, tubeContainer, reparent: false, force: true);

        if (_gameTiming.CurTime > holder.LastCrawl + TimeSpan.FromSeconds(holder.TravelDuration))
        {
            holder.LastCrawl = _gameTiming.CurTime;
            _audioSystem.PlayPredicted(holder.CrawlSound, uid, holder.ContainedEntity);
        }

        var nextTube = holder.NextTube.Value;

        holder.NextTube = null;
        holder.MoveStartTime = TimeSpan.Zero;
        holder.MoveEndTime = TimeSpan.Zero;
        Dirty(uid, holder);

        EnterTube(uid, nextTube, holder);

        if (holder.IsMoving && holder.CurrentDirection != Direction.Invalid)
        {
            var chainedNext = _ventCrawlTubeSystem.NextTubeFor(nextTube, holder.CurrentDirection);

            if (chainedNext != null && holder.CurrentTube != chainedNext)
            {
                holder.NextTube = chainedNext;
                holder.MoveStartTime = _gameTiming.CurTime;
                holder.MoveEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(holder.TravelDuration);
                Dirty(uid, holder);
            }
        }
    }

    private void UpdateManifoldPositionInterpolated(
        EntityUid manifoldUid,
        EntityUid holderUid,
        VentCrawlHolderComponent holder)
    {
        if (holder.ManifoldLayer == null)
            return;

        var totalSeconds = (holder.ManifoldTransitionEnd - holder.ManifoldTransitionStart).TotalSeconds;

        if (totalSeconds <= 0 || holder.PreviousManifoldLayer == null)
        {
            UpdateManifoldPosition(manifoldUid, holderUid, holder);
            return;
        }

        var elapsed = (_gameTiming.CurTime - holder.ManifoldTransitionStart).TotalSeconds;
        var t = (float)Math.Clamp(elapsed / totalSeconds, 0.0, 1.0);

        if (t >= 1f)
        {
            UpdateManifoldPosition(manifoldUid, holderUid, holder);
            return;
        }

        var fromOffset = GetOffsetForManifoldLayer(manifoldUid, holder.PreviousManifoldLayer.Value);
        var toOffset = GetOffsetForManifoldLayer(manifoldUid, holder.ManifoldLayer.Value);

        var lerpedOffset = Vector2.Lerp(fromOffset, toOffset, t);

        var manifoldPos = Transform(manifoldUid).Coordinates;
        _xformSystem.SetCoordinates(holderUid, _xformSystem.WithEntityId(manifoldPos.Offset(lerpedOffset), manifoldUid));
    }

    private Vector2 GetOffsetForManifoldLayer(EntityUid manifoldUid, int layerIndex)
    {
        var backTube = _ventCrawlTubeSystem.GetManifoldExit(manifoldUid, layerIndex, Direction.South);
        var frontTube = _ventCrawlTubeSystem.GetManifoldExit(manifoldUid, layerIndex, Direction.North);
        var anchor = frontTube ?? backTube;

        var pipeLayer = anchor != null && TryComp<AtmosPipeLayersComponent>(anchor, out var layersComp)
            ? layersComp.CurrentPipeLayer : SharedVentCrawlTubeSystem.TransformFromManifoldLayer(layerIndex);
        var baseOffset = GetLayerOffset(pipeLayer);
        if (baseOffset == Vector2.Zero)
            return Vector2.Zero;

        var manifoldRotation = Transform(manifoldUid).LocalRotation;
        return manifoldRotation.RotateVec(baseOffset);
    }

    private bool UpdateManifoldInput(
        EntityUid manifoldUid,
        EntityUid uid,
        VentCrawlHolderComponent holder,
        VentCrawlManifoldComponent manifold)
    {
        if (!holder.IsMoving)
            return true;

        if (holder.CurrentDirection == Direction.Invalid)
            return true;

        if (holder.ManifoldLayer == null && holder.PreviousTube != null && TryComp<AtmosPipeLayersComponent>(holder.PreviousTube, out var previousLayer))
            holder.ManifoldLayer = TransformIntoManifoldLayer(previousLayer.CurrentPipeLayer);

        holder.ManifoldLayer ??= 0;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.ManifoldLayer));

        var dir = holder.CurrentDirection;
        var manifoldRotation = Transform(manifoldUid).LocalRotation;
        var localDir = (dir.ToAngle() - manifoldRotation).GetCardinalDir();

        if (localDir is Direction.West or Direction.East)
        {
            if (_gameTiming.CurTime < holder.ManifoldLastLayerSelection + holder.ManifoldLayerSelectionCooldown)
                return true;

            holder.ManifoldLastLayerSelection = _gameTiming.CurTime;

            var delta = localDir == Direction.East ? -1 : 1;
            var newLayer = Math.Clamp(holder.ManifoldLayer.Value + delta, 0, manifold.LayerCount - 1);

            if (newLayer != holder.ManifoldLayer)
            {
                holder.PreviousManifoldLayer = holder.ManifoldLayer;
                holder.ManifoldLayer = newLayer;
                holder.ManifoldTransitionStart = _gameTiming.CurTime;
                holder.ManifoldTransitionEnd = _gameTiming.CurTime + TimeSpan.FromSeconds(holder.ManifoldTransitionDuration);
                holder.NextTube = null;
                holder.MoveStartTime = TimeSpan.Zero;
                holder.MoveEndTime = TimeSpan.Zero;
                Dirty(uid, holder);
            }
            return true;
        }

        if (localDir is Direction.North or Direction.South)
        {
            if (_gameTiming.CurTime < holder.ManifoldTransitionEnd)
                return true;

            if (holder.NextTube != null)
                return true;

            var nextTube = _ventCrawlTubeSystem.GetManifoldExit(manifoldUid, holder.ManifoldLayer.Value, dir);
            if (nextTube == null || holder.CurrentTube == nextTube)
                return true;

            holder.NextTube = nextTube;
            holder.MoveStartTime = _gameTiming.CurTime;
            holder.MoveEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(holder.TravelDuration);
            Dirty(uid, holder);
        }

        return true;
    }

    private void UpdateManifoldPosition(EntityUid manifoldUid, EntityUid holderUid, VentCrawlHolderComponent holder)
    {
        if (holder.ManifoldLayer == null)
            return;

        var backTube = _ventCrawlTubeSystem.GetManifoldExit(manifoldUid, holder.ManifoldLayer.Value, Direction.South);
        var frontTube = _ventCrawlTubeSystem.GetManifoldExit(manifoldUid, holder.ManifoldLayer.Value, Direction.North);

        var anchorTube = frontTube ?? backTube;

        var currentPipeLayer = AtmosPipeLayer.Primary;
        if (anchorTube != null && TryComp<AtmosPipeLayersComponent>(anchorTube, out var layersComp))
        {
            currentPipeLayer = layersComp.CurrentPipeLayer;
        }
        else if (holder.ManifoldLayer != null)
        {
            currentPipeLayer = SharedVentCrawlTubeSystem.TransformFromManifoldLayer(holder.ManifoldLayer.Value);
        }

        var offset = GetLayerOffset(currentPipeLayer);
        var manifoldPos = Transform(manifoldUid).Coordinates;

        _xformSystem.SetCoordinates(holderUid, _xformSystem.WithEntityId(manifoldPos.Offset(offset), manifoldUid));
    }

    public static int TransformIntoManifoldLayer(AtmosPipeLayer layer) => layer switch
    {
        AtmosPipeLayer.Primary => 2,

        AtmosPipeLayer.Secondary => 1,
        AtmosPipeLayer.Tertiary => 3,

        AtmosPipeLayer.Quaternary => 0,
        AtmosPipeLayer.Quinary => 4,

        _ => 3
    };

    public static Vector2 GetLayerOffset(AtmosPipeLayer layer) => layer switch
    {
        AtmosPipeLayer.Primary => Vector2.Zero,

        AtmosPipeLayer.Secondary => new Vector2(0.15f, 0f),
        AtmosPipeLayer.Tertiary => new Vector2(-0.15f, 0f),

        AtmosPipeLayer.Quaternary => new Vector2(0.25f, 0f),
        AtmosPipeLayer.Quinary => new Vector2(-0.25f, 0f),

        _ => Vector2.Zero
    };
}
