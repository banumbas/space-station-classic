using Content.Shared._Classic.SupplyPods;
using Content.Shared._Starlight.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Classic.SupplyPods;

/// <summary>
/// Server-side supply pod system. Provides the abstract <see cref="Deliver"/> API
/// that any system can call to deliver entities via a falling supply pod.
/// </summary>
public sealed class ClassicSupplyPodSystem : SharedClassicSupplyPodSystem
{
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ScreenshakeSystem _screenshake = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly List<PendingDelivery> _pending = new();
    private readonly HashSet<EntityUid> _impactSet = new();

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];

            // Warning -> Falling transition.
            if (!pending.FallStarted && now >= pending.FallAt)
            {
                pending.FallStarted = true;
                if (!Deleted(pending.Pod) && TryComp<ClassicSupplyPodComponent>(pending.Pod, out var fallComp))
                    SetPhase(pending.Pod, fallComp, ClassicSupplyPodPhase.Falling);
            }

            if (now < pending.LandAt)
                continue;

            LandPod(pending);
            _pending.RemoveAt(i);
        }
    }

    private void SetPhase(EntityUid pod, ClassicSupplyPodComponent comp, ClassicSupplyPodPhase phase)
    {
        comp.Phase = phase;
        Dirty(pod, comp);
        _appearance.SetData(pod, ClassicSupplyPodVisuals.Phase, phase);
    }

    /// <summary>
    /// Delivers the given payload entities to the target coordinates via a falling
    /// supply pod. Any system can call this.
    /// </summary>
    public EntityUid Deliver(
        EntityCoordinates coordinates,
        List<EntityUid>? payload = null,
        ClassicSupplyPodVisual visual = ClassicSupplyPodVisual.Default,
        EntProtoId? podPrototype = null,
        bool openOnLand = true,
        float despawnTime = 0f)
    {
        if (!coordinates.IsValid(EntityManager))
            return EntityUid.Invalid;

        var proto = podPrototype ?? DefaultPodPrototype;
        var pod = Spawn(proto, coordinates);
        if (!TryComp<ClassicSupplyPodComponent>(pod, out var podComp))
        {
            Log.Error($"Supply pod prototype {proto} missing ClassicSupplyPodComponent!");
            QueueDel(pod);
            return EntityUid.Invalid;
        }

        podComp.Visual = visual;
        podComp.AutoOpen = openOnLand;
        podComp.DespawnTime = despawnTime;

        SetPhase(pod, podComp, ClassicSupplyPodPhase.Warning);

        // Disable collision while in the air so the pod doesn't block entities at
        // the landing location before it has actually landed.
        if (TryComp<PhysicsComponent>(pod, out var physics))
            _physics.SetCanCollide(pod, false, body: physics);

        // Insert payload and stun mob passengers for the entire fall so they can't
        // interact with or open the storage.
        if (TryComp<EntityStorageComponent>(pod, out var storage))
        {
            if (payload != null)
            {
                foreach (var ent in payload)
                {
                    if (Deleted(ent))
                        continue;
                    _storage.Insert(ent, pod);
                }
            }

            var preLandStun = TimeSpan.FromSeconds(podComp.FallDuration + podComp.PreLandStunTime);
            foreach (var contained in storage.Contents.ContainedEntities)
            {
                if (Deleted(contained) || !HasComp<MobStateComponent>(contained))
                    continue;
                _stun.TryAddParalyzeDuration(contained, preLandStun);
            }
        }

        Spawn(podComp.TargetIndicatorProto, coordinates);

        if (podComp.LaunchSound != null)
            Audio.PlayPvs(podComp.LaunchSound, pod);

        var now = _timing.CurTime;
        var landAt = now + TimeSpan.FromSeconds(podComp.FallDuration);
        var fallAt = landAt - TimeSpan.FromSeconds(podComp.FallAnimationLeadTime);
        if (fallAt < now)
            fallAt = now;

        _pending.Add(new PendingDelivery
        {
            Pod = pod,
            LandAt = landAt,
            FallAt = fallAt,
        });

        return pod;
    }

    private void LandPod(PendingDelivery pending)
    {
        var pod = pending.Pod;
        if (Deleted(pod) || !TryComp<ClassicSupplyPodComponent>(pod, out var podComp))
            return;

        SetPhase(pod, podComp, ClassicSupplyPodPhase.Landed);

        // Capture entities in the impact radius BEFORE re-enabling collision,
        // otherwise the physics engine may push mobs away before damage is applied.
        _impactSet.Clear();
        _lookup.GetEntitiesInRange(pod, podComp.ImpactRadius, _impactSet,
            LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Contained);

        // Also find mobs (MobHuman and similar) via a component-based lookup.
        // The entity-based range query above can miss mobs standing on the landing
        // tile (e.g. when the pod is anchored/static), so we explicitly collect all
        // entities with MobStateComponent in the impact radius. This covers a 3x3
        // grid (radius 1). Mobs inside the pod's storage are excluded later in
        // ApplyImpactDamage.
        //
        // LookupFlags.Approximate is CRITICAL here: without it, the generic
        // component query performs a precise fixture-overlap test (TestOverlap)
        // that frequently fails to catch mobs whose small fixtures don't strictly
        // intersect the query circle. Approximate uses AABB-only checks, matching
        // the behaviour of the non-generic query above that successfully finds
        // structures.
        var podCoords = Transform(pod).Coordinates;
        var mobSet = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(podCoords, podComp.ImpactRadius, mobSet,
            LookupFlags.Approximate | LookupFlags.Dynamic);
        foreach (var mob in mobSet)
        {
            _impactSet.Add(mob);
        }

        // Apply all damage/effects FIRST, before re-enabling collision. This ensures
        // mobs caught under the pod are damaged before any physics pushback can
        // interfere (e.g. via events triggered by collision).
        ApplyImpactDamage(pod, podComp);
        ApplyPassengerEffects(pod, podComp);

        // Now re-enable collision.
        if (TryComp<PhysicsComponent>(pod, out var physics))
            _physics.SetCanCollide(pod, true, body: physics);

        if (podComp.ImpactSound != null)
            Audio.PlayPvs(podComp.ImpactSound, pod);

        if (podComp.ImpactEffect != null)
            Spawn(podComp.ImpactEffect, Transform(pod).Coordinates);

        if (podComp.AutoOpen)
        {
            Timer.Spawn(TimeSpan.FromSeconds(podComp.OpenDelay), () =>
            {
                if (!Deleted(pod))
                    _storage.OpenStorage(pod);
            });
        }

        if (podComp.DespawnTime > 0)
        {
            Timer.Spawn(TimeSpan.FromSeconds(podComp.DespawnTime), () =>
            {
                if (!Deleted(pod))
                    QueueDel(pod);
            });
        }
    }

    /// <summary>
    /// Applies area damage to entities around the pod on landing. The pod itself
    /// and its contents are excluded.
    /// </summary>
    private void ApplyImpactDamage(EntityUid pod, ClassicSupplyPodComponent comp)
    {
        // Never damage the pod itself.
        _impactSet.Remove(pod);

        // Exclude the pod's contents (passengers/cargo) — they are handled by
        // ApplyPassengerEffects.
        if (TryComp<EntityStorageComponent>(pod, out var storage))
        {
            foreach (var contained in storage.Contents.ContainedEntities)
                _impactSet.Remove(contained);
        }

        foreach (var ent in _impactSet)
        {
            if (Deleted(ent))
                continue;

            // Skip items and entities without DamageableComponent.
            if (HasComp<ItemComponent>(ent) || !HasComp<DamageableComponent>(ent))
                continue;

            // Clone the DamageSpecifier so resistance/modifier calculations on one
            // entity don't corrupt the shared component field for the next entity.
            _damageable.TryChangeDamage(ent, new DamageSpecifier(comp.ImpactDamage), origin: pod);
        }
    }

    /// <summary>
    /// Stuns, damages and screenshakes the mob passengers inside the pod on impact.
    /// </summary>
    private void ApplyPassengerEffects(EntityUid pod, ClassicSupplyPodComponent comp)
    {
        if (!TryComp<EntityStorageComponent>(pod, out var storage))
            return;

        var shakeTranslation = new ScreenshakeParameters { Trauma = 0.6f, DecayRate = 1.5f, Frequency = 12f };

        foreach (var contained in storage.Contents.ContainedEntities)
        {
            if (Deleted(contained) || !HasComp<MobStateComponent>(contained))
                continue;

            var brute = _random.NextFloat(comp.PassengerMinBrute, comp.PassengerMaxBrute);
            var damage = new DamageSpecifier
            {
                DamageDict = new() { { "Blunt", brute } }
            };
            _damageable.TryChangeDamage(contained, damage, origin: pod);
            _screenshake.Screenshake(contained, shakeTranslation, null);
        }
    }

    private sealed class PendingDelivery
    {
        public EntityUid Pod;
        public TimeSpan LandAt;
        public TimeSpan FallAt;
        public bool FallStarted;
    }
}
