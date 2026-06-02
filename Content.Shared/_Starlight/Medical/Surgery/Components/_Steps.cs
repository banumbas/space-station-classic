using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
namespace Content.Shared.Starlight.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryClampBleedEffectComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryStepAttachLimbEffectComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryStepBleedEffectComponent : Component
{
    [DataField]
    public DamageSpecifier? Damage;
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryStepAmputationEffectComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryRemoveAccentComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class SurgeryClearProgressComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepEmoteEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> Emote = "Scream";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepSpawnEffectComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Entity;
}

[RegisterComponent]
public sealed partial class SurgeryStepPenaltiesComponent : Component
{
    [DataField]
    public float SelfSurgeryPenalty = 0.5f;

    [DataField]
    public float NotSleepingPenalty = 0.7f;

    [DataField]
    public float DrunkPenalty = 0.5f;

    [DataField]
    public float DepartmentPenalty = 1.0f; // Temporary disabled, should be 20% penalty: Requires skill system and update in jobs system(so when HOP changes your access, he can change your job)

    [DataField]
    public float JobBonus = 1.0f; // Temporary disabled, should be 20% bonus: Requires skill system and update in jobs system(so when HOP changes your access, he can change your job)

    [DataField]
    public float NoGlovesPenalty = 0.9f;

    [DataField]
    public float ClumsyPenalty = 0.75f;

    [DataField]
    public List<string> AllowedDepartments = new()
    {
        "Medical",
    };

    [DataField]
    public List<string> BonusedJobs = new()
    {
        "Surgeon",
    };

    [DataField]
    public string FallbackJob = "Passenger";

    [DataField]
    public string GlovesSlot = "gloves";

    [DataField]
    public string DrunkStatusEffect = "StatusEffectDrunk";
}
