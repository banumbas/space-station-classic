global using XenoComponent = Content.Shared._Starlight.Antags.Xeno.Components.XenoComponent;

using System;
using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Classic.Vehicles;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class SkillWhitelist
{
    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> All = new();
}

[RegisterComponent]
public sealed partial class SkillDefinitionComponent : Component
{
}


[Serializable, NetSerializable]
public enum VehicleMobSize : byte
{
    Small,
    Normal,
    Big,
    Immobile
}


[ByRefEvent]
public record struct DamageModifyEvent(DamageSpecifier Damage, EntityUid? Origin = null, EntityUid? Tool = null);

[ByRefEvent]
public record struct ExplosionReceivedEvent(DamageSpecifier Damage);

[ByRefEvent]
public record struct AttemptShootEvent(
    EntityUid User,
    Entity<GunComponent> Gun = default,
    string? Message = null,
    EntityCoordinates FromCoordinates = default,
    EntityCoordinates? ToCoordinates = null,
    bool Cancelled = false,
    bool ThrowItems = false,
    bool ResetCooldown = false)
{
    public bool Cancelled { get; set; } = Cancelled;
    public bool ResetCooldown { get; set; } = ResetCooldown;
    public EntityCoordinates FromCoordinates { get; set; } = FromCoordinates;
    public EntityCoordinates? ToCoordinates { get; set; } = ToCoordinates;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunSpinupComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BaseShotDelay = 0.7f;

    [DataField, AutoNetworkedField]
    public float BaseScatter = 18f;

    [DataField, AutoNetworkedField]
    public float SpinUpTime = 10f;

    [DataField, AutoNetworkedField]
    public float GraceAfterStop = 2f;

    [DataField, AutoNetworkedField]
    public float SpinDownTime = 3f;

    [DataField, AutoNetworkedField]
    public float MinSpinLevel = 1f;

    [DataField, AutoNetworkedField]
    public float MaxSpinLevel = 11f;

    [DataField, AutoNetworkedField]
    public int[] RateTiers = [1, 1, 2, 2, 3, 3, 3, 4, 4, 4, 5];

    [DataField, AutoNetworkedField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_Classic/Vehicle/weapons/minigun_start.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LoopSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/minigun.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? StopSound = new SoundPathSpecifier("/Audio/_Classic/Vehicle/weapons/minigun_stop.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SelectSound = new SoundPathSpecifier("/Audio/_Classic/Vehicle/weapons/minigun_select.ogg");

    [DataField, AutoNetworkedField]
    public float LoopSoundCooldown = 0.2f;

    [DataField, AutoNetworkedField]
    public float FireWindowPadding = 0.12f;

    [DataField, AutoNetworkedField]
    public float InitialWindupDelay = 0f;

    [DataField, AutoNetworkedField]
    public float InitialWindupResetGap = 0.2f;
}

[ByRefEvent]
public record struct GetWeaponAccuracyEvent(
    FixedPoint2 AccuracyMultiplier,
    float Range
);

[ByRefEvent]
public record struct GetIFFGunUserEvent(EntityUid? GunUser = null)
{
    public EntityUid? GunUser { get; set; } = GunUser;
}


[RegisterComponent]
public sealed partial class BarricadeComponent : Component
{
}

[KeyFunctions]
public static class VehicleKeyFunctions
{
    public static readonly BoundKeyFunction VehicleUniqueAction = "VehicleUniqueAction";
}


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleFlamerAmmoProviderComponent : Component, IShootable
{
    [DataField, AutoNetworkedField]
    public string ContainerId = "gun_magazine";

    [DataField, AutoNetworkedField]
    public TimeSpan DelayPer = TimeSpan.FromSeconds(0.05);

    [DataField, AutoNetworkedField]
    public FixedPoint2 CostPer = FixedPoint2.New(1);

    [DataField, AutoNetworkedField]
    public TimeSpan CantShootPopupCooldown = TimeSpan.FromSeconds(0.25);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleFlamerTankComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SolutionId = "vehicle_flamer_tank";

    [DataField, AutoNetworkedField]
    public int MaxIntensity = 40;

    [DataField, AutoNetworkedField]
    public int MaxDuration = 30;

    [DataField, AutoNetworkedField]
    public int MaxRange = 5;
}

[ByRefEvent]
public record struct BeforeAttemptShootEvent(
    EntityCoordinates Origin,
    System.Numerics.Vector2 Offset = default,
    bool Handled = false);

[RegisterComponent, NetworkedComponent]
public sealed partial class RemoveComponentsComponent : Component
{
    [DataField]
    public ComponentRegistry? Components;
}

public sealed class RemoveComponentsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RemoveComponentsComponent, ComponentInit>(OnRemoveComponentsInit);
    }

    private void OnRemoveComponentsInit(Entity<RemoveComponentsComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Components != null)
            EntityManager.RemoveComponents(ent, ent.Comp.Components);
    }
}
