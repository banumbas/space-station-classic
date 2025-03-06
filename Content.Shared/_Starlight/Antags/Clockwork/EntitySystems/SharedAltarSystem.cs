using Content.Shared.Buckle.Components;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared._Starlight.Antags.Cults.Clockwork;

namespace Content.Shared.Starlight.Antags.Clockwork.EntitySystems;

public abstract class SharedAltarSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<AltarComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<AltarComponent, StrapAttemptEvent>(OnStrapAttempt);
        base.Initialize();
    }
    
    private void OnUnstrapAttempt(EntityUid uid, AltarComponent component, ref UnstrapAttemptEvent args)
    {
        if (args.User == null || CanBeCultist(args.User.Value))
            args.Cancelled = true;
    }
    
    private void OnStrapAttempt(EntityUid uid, AltarComponent component, ref StrapAttemptEvent args)
    {
        if (args.User == null || !CanBeCultist(args.User.Value))
            args.Cancelled = true;
    }
    
    public bool CanBeCultist(EntityUid uid)
    {
        if (HasComp<ClockworkCultistComponent>(uid) || HasComp<ClockworkMasterComponent>(uid))
            return false;
        
        return true;
    }
}