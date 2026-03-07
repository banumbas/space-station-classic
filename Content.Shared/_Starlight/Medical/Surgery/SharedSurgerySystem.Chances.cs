using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.StatusEffectNew;
using Content.Shared.Mind;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Bed.Sleep;
using Content.Shared.Traits.Assorted;
using Content.Shared.Clumsy;
using Content.Shared.Silicons.Borgs.Components;
using System.Linq;
using Content.Shared.Humanoid;

namespace Content.Shared.Starlight.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    private void InitializeChances()
    {
        SubscribeLocalEvent<AbductorComponent, OperationChanceEvent>(OnAbductorOperationChance);
        SubscribeLocalEvent<SurgeryToolComponent, OperationChanceEvent>(OnSurgeryToolOperationChance);
        SubscribeLocalEvent<ClumsyComponent, OperationChanceEvent>(OnClumsyOperationChance);
        SubscribeLocalEvent<HumanoidAppearanceComponent, OperationChanceEvent>(OnHumanoidOperationChance);
    }

    private void OnHumanoidOperationChance(EntityUid uid, HumanoidAppearanceComponent component, ref OperationChanceEvent args)
    {

        if (args.Target == uid)
        {
            if (args.Performer == uid)
            {
                args.Chance = Math.Clamp(args.Chance * 0.5f, 0.0f, 1.0f); // 50% penalty for self-surgery
                args.Reason = "You are performing surgery on yourself, so your make mistakes. You need to start this step all over again!";
            }

            if (!HasComp<SleepingComponent>(uid) && !HasComp<PainNumbnessStatusEffectComponent>(args.Target))
            {
                args.Chance = Math.Clamp(args.Chance * 0.7f, 0.0f, 1.0f); // 30% penalty for not sleeping patients
                args.Reason = "The patient is not fully unconscious, so they moved during the surgery. You need to start this step all over again!";
            }
        }
        else if (args.Performer == uid)
        {
            if (_statusEffects.HasStatusEffect(uid, "StatusEffectDrunk"))
            {
                args.Chance = Math.Clamp(args.Chance * 0.50f, 0.0f, 1.0f); // 50% penalty for drunk surgeons
                args.Reason = "Being intoxicated affected your precision during the surgery. You need to start this step all over again!";
            }

            if (_mind.TryGetMind(uid, out _, out var mind))
            {
                bool nonMedicalDepartment = true;
                string jobId = "Passenger";
                if (mind.MindRoleContainer.ContainedEntities.Count > 0)
                    foreach (var roleId in mind.MindRoleContainer.ContainedEntities)
                    {
                        if (!HasComp<JobRoleComponent>(roleId)
                            || !TryComp<MindRoleComponent>(roleId, out var mindRole)
                            || mindRole.JobPrototype == null
                            || !_job.TryGetDepartment(mindRole.JobPrototype, out var department)
                            || department.ID != "Medical")
                            continue;

                        nonMedicalDepartment = false;
                        jobId = mindRole.JobPrototype;
                        break;
                    }
                else
                    nonMedicalDepartment = false;

                bool isMedicalBorg = TryComp<BorgSwitchableTypeComponent>(uid, out var borg) && borg.SelectedBorgType == "medical";

                if (nonMedicalDepartment && !isMedicalBorg)
                    args.Chance = Math.Clamp(args.Chance * 0.8f, 0.0f, 1.0f); // 20% penalty for non-medical roles
                else if (jobId == "Surgeon" || isMedicalBorg)
                    args.Chance = Math.Clamp(args.Chance * 1.2f, 0.0f, 1.0f); // 20% bonus for surgeons or medical borgs
            }

            if (_inventory.TryGetSlotContainer(uid, "gloves", out var container, out _)
                && container.ContainedEntities.Count() == 0)
                args.Chance = Math.Clamp(args.Chance * 0.90f, 0.0f, 1.0f); // 10% penalty for not wearing gloves
        }
    }

    private void OnClumsyOperationChance(EntityUid uid, ClumsyComponent component, ref OperationChanceEvent args)
    {
        if (args.Performer != uid)
            return;
        args.Chance = Math.Clamp(args.Chance * 0.75f, 0.0f, 1.0f); // 25% penalty for clumsy surgeons
        args.Reason = "Due to your clumsiness, you made a mistake during the surgery. You need to start this step all over again!";
    }

    private void OnAbductorOperationChance(EntityUid uid, AbductorComponent component, ref OperationChanceEvent args)
    {
        if (args.Performer != uid)
            return;
        args.ForceSuccess = true; //Abductors always succeed, because they aliens.
    }

    private void OnSurgeryToolOperationChance(EntityUid uid, SurgeryToolComponent component, ref OperationChanceEvent args)
    {
        if (args.Tool != uid)
            return;
        args.Chance = MathF.Sqrt(args.Chance * component.SuccessRate);
    }

    public float CalculateStepSuccessRate(EntityUid user, EntityUid body, EntityUid step, EntityUid tool, out string reason)
    {
        float successRate = 1f;
        reason = "";

        if (!TryComp<SurgeryStepComponent>(step, out var stepComp))
            return 1f;

        successRate = ((int)stepComp.Difficulty) / 100f; // Convert from enum to float 0.0 - 1.0

        var @event = new OperationChanceEvent(user, body, tool, successRate);
        RaiseLocalEvent(user, ref @event);
        RaiseLocalEvent(body, ref @event);
        RaiseLocalEvent(tool, ref @event);

        if (@event.ForceSuccess)
            return 1f;

        Log.Debug($"Real surgery step chance to success: {@event.Chance * 100f}%, ForceSuccess: {@event.ForceSuccess}, step difficulty: {stepComp.Difficulty}");

        return @event.Chance;
    }
}