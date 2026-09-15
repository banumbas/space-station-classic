/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Classic.ZLevels.Core.EntitySystems;

public abstract partial class ClassicSharedZLevelsSystem
{
    public int UpdateCalls { get; private set; }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateCalls = 0;
        FlushMovementBodyRefreshes();

        if (_net.IsClient && !_clientSimulation)
            return;

        _accumulatedTime += TimeSpan.FromSeconds(frameTime);

        var steps = 0;
        while (_accumulatedTime >= _fixedTimestep && steps < MaxStepsPerFrame)
        {
            UpdateZPhysics((float) _fixedTimestep.TotalSeconds);
            _accumulatedTime -= _fixedTimestep;

            steps++;
        }
    }

    private void UpdateZPhysics(float frameTime)
    {
        UpdateDirtyMovement();

        // Landing, damage and map transitions may wake/sleep other bodies during this loop.
        // A reused snapshot keeps iteration stable while membership changes remain O(1).
        _activeBodySnapshot.Clear();
        _activeBodySnapshot.AddRange(_activeBodies);
        for (var i = _activeBodySnapshot.Count - 1; i >= 0; i--)
        {
            var uid = _activeBodySnapshot[i];
            if (!_activeBodies.Contains(uid))
                continue;

            if (!ZPhysicsQuery.TryComp(uid, out var zPhysicsComponent) ||
                !_transformQuery.TryComp(uid, out var xform) ||
                !_physicsQuery.TryComp(uid, out var physics))
            {
                _activeBodies.Remove(uid);
                continue;
            }

            if (!_zMapQuery.HasComp(xform.MapUid))
            {
                _activeBodies.Remove(uid);
                continue;
            }

            ProcessZPhysics((uid, zPhysicsComponent, physics), frameTime);
        }
    }

    private void ProcessZPhysics(Entity<ClassicZPhysicsComponent, PhysicsComponent> entity, float frameTime)
    {
        var zPhysicsComponent = entity.Comp1;

        if (zPhysicsComponent.Disabled)
            return;

        UpdateCalls++;

        var oldVelocity = zPhysicsComponent.Velocity;
        var oldHeight = zPhysicsComponent.LocalPosition;

        if (zPhysicsComponent.VelocityGravity)
            zPhysicsComponent.Velocity -= ZGravityForce * zPhysicsComponent.GravityMultiplier * frameTime;

        if (zPhysicsComponent.VelocityRaiseEvent)
        {
            var velocityEvent = new ClassicGetZVelocityEvent((entity, zPhysicsComponent));
            RaiseLocalEvent(entity, ref velocityEvent);

            zPhysicsComponent.Velocity += velocityEvent.VelocityDelta * frameTime;
        }

        zPhysicsComponent.LocalPosition += zPhysicsComponent.Velocity * frameTime;
        var distanceToGround = zPhysicsComponent.LocalPosition - zPhysicsComponent.CachedGroundHeight;

        if (zPhysicsComponent.AutoStep && distanceToGround < 0)
            zPhysicsComponent.LocalPosition -= distanceToGround;

        if (zPhysicsComponent.CachedStickyGround)
            zPhysicsComponent.LocalPosition -= distanceToGround;

        if (zPhysicsComponent is { Velocity: < 0, Fallable: true })
        {
            if (distanceToGround <= 0.05f)
            {
                if (float.Abs(zPhysicsComponent.Velocity) >= ImpactVelocityLimit)
                {
                    var hitEv = new ClassicZLevelHitEvent(-zPhysicsComponent.Velocity);
                    RaiseLocalEvent(entity, ref hitEv);

                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(entity, ref land);
                }

                if (float.Abs(zPhysicsComponent.Velocity) < zPhysicsComponent.SleepThreshold)
                {
                    zPhysicsComponent.Velocity = 0;
                    zPhysicsComponent.LocalPosition = zPhysicsComponent.CachedGroundHeight;
                }
                else
                {
                    zPhysicsComponent.Velocity = -zPhysicsComponent.Velocity * zPhysicsComponent.Bounciness;
                }
            }
        }

        if (zPhysicsComponent.LocalPosition < 0)
        {
            if (TryMoveDownOrChasm(entity))
            {
                zPhysicsComponent.LocalPosition += 1;
                if (zPhysicsComponent is { CachedStickyGround: false, Fallable: true })
                {
                    var fallEv = new ClassicZLevelFallMapEvent();
                    RaiseLocalEvent(entity, ref fallEv);
                }
            }
        }

        if (zPhysicsComponent.LocalPosition >= 1)
        {
            if (HasTileAbove(entity))
            {
                if (float.Abs(zPhysicsComponent.Velocity) >= ImpactVelocityLimit)
                {
                    var hitEv = new ClassicZLevelHitEvent(zPhysicsComponent.Velocity);
                    RaiseLocalEvent(entity, ref hitEv);

                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(entity, ref land);
                }
                zPhysicsComponent.LocalPosition = 1;
                zPhysicsComponent.Velocity = -zPhysicsComponent.Velocity * zPhysicsComponent.Bounciness;
            }
            else
            {
                if (TryMoveUp(entity))
                    zPhysicsComponent.LocalPosition -= 1;
            }
        }

        if (float.Abs(zPhysicsComponent.Velocity) > ZVelocityLimit)
            zPhysicsComponent.Velocity = float.Sign(zPhysicsComponent.Velocity) * ZVelocityLimit;

        if (float.Abs(oldVelocity - zPhysicsComponent.Velocity) > 0.001f)
            DirtyField(entity, zPhysicsComponent, nameof(ClassicZPhysicsComponent.Velocity));

        if (oldHeight != zPhysicsComponent.LocalPosition)
        {
            OnZPositionChanged((entity.Owner, zPhysicsComponent));

            if (float.Abs(oldHeight - zPhysicsComponent.LocalPosition) > 0.001f)
                DirtyField(entity, zPhysicsComponent, nameof(ClassicZPhysicsComponent.LocalPosition));
        }

        if (zPhysicsComponent.VelocityGravity)
        {
            var targetStatus = distanceToGround > AirborneHeightThreshold ? BodyStatus.InAir : BodyStatus.OnGround;
            if (entity.Comp2.BodyStatus != targetStatus)
            {
                _physicsSystem.SetBodyStatus(entity, entity.Comp2, targetStatus);
                var statusEv = new ClassicZBodyStatusChangedEvent(targetStatus);
                RaiseLocalEvent(entity, ref statusEv);
            }
        }

        SleepUpdate((entity, entity.Comp1), frameTime);
    }

    private void SleepUpdate(Entity<ClassicZPhysicsComponent> entity, float frameTime)
    {
        var distanceToGround = entity.Comp.LocalPosition - entity.Comp.CachedGroundHeight;
        var almostStopped = float.Abs(entity.Comp.Velocity) < entity.Comp.SleepThreshold && float.Abs(distanceToGround) <= 0.01f;

        if (!almostStopped)
        {
            entity.Comp.SleepTimer = 0f;
            return;
        }

        entity.Comp.SleepTimer += frameTime;
        if (entity.Comp.SleepTimer < entity.Comp.TimeToSleep)
            return;

        SleepBody((entity, entity));
    }
}

/// <summary>
/// Raised directed on an entity when its BodyStatus changes due to Z-physics height sync.
/// </summary>
[ByRefEvent]
public readonly record struct ClassicZBodyStatusChangedEvent(BodyStatus NewStatus);
