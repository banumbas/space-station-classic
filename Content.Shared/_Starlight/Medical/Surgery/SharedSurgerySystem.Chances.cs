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
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    private void InitializeChances()
    {
        SubscribeLocalEvent<AbductorComponent, OperationChanceEvent>(OnAbductorOperationChance);
        SubscribeLocalEvent<SurgeryToolComponent, OperationChanceEvent>(OnSurgeryToolOperationChance);
        SubscribeLocalEvent<ClumsyComponent, OperationChanceEvent>(OnClumsyOperationChance);
        SubscribeLocalEvent<HumanoidAppearanceComponent, OperationChanceEvent>(OnHumanoidOperationChance);
    }

    private void OnHumanoidOperationChance(EntityUid uid, HumanoidAppearanceComponent component, ref OperationChanceEvent args)
    {

        if (args.Performer == uid)
        {
            args.Chance = Math.Clamp(args.Chance * args.Penalties.SelfSurgeryPenalty, 0.0f, 1.0f); // penalty for self-surgery
            args.Reason = "You are performing surgery on yourself, so you make mistakes. You need to start this step all over again!";
        }

        if (args.Target == uid && !HasComp<SleepingComponent>(uid) && !HasComp<PainNumbnessStatusEffectComponent>(args.Target))
        {
            args.Chance = Math.Clamp(args.Chance * args.Penalties.NotSleepingPenalty, 0.0f, 1.0f); // penalty for not sleeping patients
            args.Reason = "The patient is not fully unconscious, so they moved during the surgery. You need to start this step all over again!";
        }

        if (args.Performer == uid && _statusEffects.HasStatusEffect(uid, args.Penalties.DrunkStatusEffect))
        {
            args.Chance = Math.Clamp(args.Chance * args.Penalties.DrunkPenalty, 0.0f, 1.0f); // penalty for drunk surgeons
            args.Reason = "Being intoxicated affected your precision during the surgery. You need to start this step all over again!";
        }

        if (args.Target != args.Performer && args.Target == uid)
            return;

        if (_mind.TryGetMind(uid, out _, out var mind))
        {
            var nonMedicalDepartment = true;
            var jobId = args.Penalties.FallbackJob;
            if (mind.MindRoleContainer.ContainedEntities.Count > 0)
                foreach (var roleId in mind.MindRoleContainer.ContainedEntities)
                {
                    if (!HasComp<JobRoleComponent>(roleId)
                        || !TryComp<MindRoleComponent>(roleId, out var mindRole)
                        || mindRole.JobPrototype == null)
                        continue;

                    jobId = mindRole.JobPrototype;

                    if (_job.TryGetDepartment(mindRole.JobPrototype, out var department) && args.Penalties.AllowedDepartments.Contains(department.ID))
                        nonMedicalDepartment = false;

                    break;
                }

            var isMedicalBorg = TryComp<BorgSwitchableTypeComponent>(uid, out var borg) && args.Penalties.AllowedDepartments.Any(x => borg.SelectedBorgType == x.ToLower());

            if (nonMedicalDepartment && !isMedicalBorg)
                args.Chance = Math.Clamp(args.Chance * args.Penalties.DepartmentPenalty, 0.0f, 1.0f); // penalty for non-allowed departments
            else if (args.Penalties.BonusedJobs.Contains(jobId) || isMedicalBorg)
                args.Chance = Math.Clamp(args.Chance * args.Penalties.JobBonus, 0.0f, 1.0f); // bonus for surgeons or medical borgs
        }

        if (_inventory.TryGetSlotContainer(uid, args.Penalties.GlovesSlot, out var container, out _)
            && container.ContainedEntities.Count == 0)
        {
            args.Chance = Math.Clamp(args.Chance * args.Penalties.NoGlovesPenalty, 0.0f, 1.0f); // penalty for not wearing gloves
            args.Reason = "Due to gloves missing on your hands, you're getting blood on your hands, which is why your hand is slipping!";
        }
    }

    private void OnClumsyOperationChance(EntityUid uid, ClumsyComponent component, ref OperationChanceEvent args)
    {
        if (args.Performer != uid) return; // Clumsy decreases chances only for performers
        args.Chance = Math.Clamp(args.Chance * args.Penalties.ClumsyPenalty, 0.0f, 1.0f);
        args.Reason = "Due to your clumsiness, you made a mistake during the surgery. You need to start this step all over again!";
    }

    private void OnAbductorOperationChance(EntityUid uid, AbductorComponent component, ref OperationChanceEvent args)
        => args.ForceSuccess = true; //Abductors always succeed, because they aliens.

    private void OnSurgeryToolOperationChance(EntityUid uid, SurgeryToolComponent component, ref OperationChanceEvent args)
        => args.Chance = TryGetBehavior(uid, args.Step) is { } behavior ? (float)Math.Sqrt(args.Chance * behavior.SuccessRate) : args.Chance;

    public float CalculateStepSuccessRate(EntityUid user, EntityUid body, EntityUid step, EntityUid? tool, out string reason)
    {
        float successRate = 1f;
        reason = "";

        if (!TryComp<SurgeryStepComponent>(step, out var stepComp))
            return 1f;

        if (!TryComp<SurgeryStepPenaltiesComponent>(step, out var penalties))
            return 1f;

        successRate = ((int)stepComp.Difficulty) / 100f; // Convert from enum to float 0.0 - 1.0

        var @event = new OperationChanceEvent(user, body, tool, step, penalties, successRate);
        if (user != body)
            RaiseLocalEvent(user, ref @event);
        RaiseLocalEvent(body, ref @event);
        if (tool != null)
            RaiseLocalEvent(tool.Value, ref @event);

        if (@event.ForceSuccess)
            return 1f;

#if DEBUG
        Log.Debug($"Real surgery step chance to success: {@event.Chance * 100f}%, ForceSuccess: {@event.ForceSuccess}, step difficulty: {stepComp.Difficulty}");
#endif

        return @event.Chance;
    }
}
