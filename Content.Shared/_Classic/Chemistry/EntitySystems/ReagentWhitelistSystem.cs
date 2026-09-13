using Content.Shared._Classic.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;

namespace Content.Shared._Classic.Chemistry.EntitySystems;

/// <summary>
/// Universal system that intercepts solution transfer attempts and rejects unauthorized chemicals
/// based on the target entity's <see cref="ReagentWhitelistComponent"/>.
/// </summary>
public sealed class ReagentWhitelistSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReagentWhitelistComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
    }

    private void OnTransferAttempt(Entity<ReagentWhitelistComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner)
            return;

        var incomingSol = args.SolutionEntity.Comp.Solution;
        if (incomingSol == null || incomingSol.Contents.Count == 0)
            return;

        foreach (var reagent in incomingSol.Contents)
        {
            if (!ent.Comp.Whitelist.Contains(reagent.Reagent.Prototype))
            {
                args.Cancel(Loc.GetString(ent.Comp.DeniedPopup));
                return;
            }
        }
    }
}
