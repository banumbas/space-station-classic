using Content.Shared.UserInterface;
using Content.Shared.Whitelist;

namespace Content.Shared._Classic.Vendors;

public abstract class SharedClassicAutomatedVendorSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClassicAutomatedVendorComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<ClassicAutomatedVendorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.UserWhitelist != null && _whitelist.IsWhitelistFail(ent.Comp.UserWhitelist, args.User))
        {
            args.Cancel();
        }
    }
}
