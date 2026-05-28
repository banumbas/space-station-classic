using System.Numerics;
using Content.Shared._Starlight.Eye.Blinding.Components;
using Content.Shared._Starlight.VentCrawl.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Item;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

public sealed partial class SharedVentCrawlSystem
{
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
    /// Computes the world-space target position for a tube (center + pipe layer offset, rotated by grid).
    /// </summary>
    private Vector2 ComputeTubeWorldPos(EntityUid tubeUid)
    {
        var worldPos = _xformSystem.GetWorldPosition(tubeUid);

        if (!TryComp<AtmosPipeLayersComponent>(tubeUid, out var layers))
            return worldPos;

        var layerOffset = GetLayerOffset(layers.CurrentPipeLayer);
        if (layerOffset == Vector2.Zero)
            return worldPos;

        return worldPos + _xformSystem.GetWorldRotation(tubeUid).RotateVec(layerOffset);
    }

    private void UpdateVisionBlocking(
        EntityUid holderUid,
        EntityUid player,
        EntityUid tubeUid)
    {
        if (!TryComp<VentCrawlTubeComponent>(tubeUid, out var tube))
            return;

        if (tube.BlocksVision)
            EnsureComp<ChildBlockVisionComponent>(holderUid);
        else
            RemComp<ChildBlockVisionComponent>(holderUid);

        if (HasComp<ParentCanBlockVisionComponent>(player))
            _blindable.UpdateIsBlind(player, true);
    }

    private void UpdateExitAction(VentCrawlHolderComponent holder, EntityUid? tubeUid = null)
    {
        var welded = false;
        if (TryComp<WeldableComponent>(tubeUid, out var weldableComponent))
            welded = weldableComponent.IsWelded;

        if (!welded && HasComp<VentCrawlEntryComponent>(tubeUid))
        {
            if (holder.ProvidedAction == null)
                holder.ProvidedAction = _actionsSystem.AddAction(holder.ContainedEntity, holder.ActionProto);
        }
        else if (holder.ProvidedAction != null)
        {
            _actionsSystem.RemoveAction(holder.ProvidedAction);
            holder.ProvidedAction = null;
        }
    }

    /// <summary>
    /// Starts movement toward <paramref name="nextTube"/>, capturing the current visual position
    /// as the animation start for seamless chaining.
    /// </summary>
    private void BeginMoveTo(
        EntityUid holderUid,
        EntityUid nextTube,
        Vector2 fromWorldPos,
        VentCrawlHolderComponent holder,
        float speed)
    {
        holder.MoveFromWorldPos = fromWorldPos;
        holder.MoveToWorldPos = ComputeTubeWorldPos(nextTube);

        holder.NextTube = nextTube;
        holder.MoveStartTime = _gameTiming.CurTime;
        holder.MoveEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(speed);

        Dirty(holderUid, holder);
    }

    private void BeginMoveTo(
        EntityUid holderUid,
        EntityUid nextTube,
        VentCrawlHolderComponent holder,
        float speed)
    {
        BeginMoveTo(
            holderUid,
            nextTube,
            _xformSystem.GetWorldPosition(holderUid),
            holder,
            speed);
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
