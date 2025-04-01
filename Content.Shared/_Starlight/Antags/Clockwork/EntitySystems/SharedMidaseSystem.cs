using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared.Starlight.Antags.Clockwork.EntitySystems;

public abstract class SharedMidaseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _net = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<MidaseUserComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MidaseUserComponent, ComponentShutdown>(OnShutdown);
        base.Initialize();
    }
    
    private void OnStartup(EntityUid uid, MidaseUserComponent component, ref ComponentStartup args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;
        
        Logger.DebugS("Midase", "MIDASE Trying to insert action");
        
        _action.AddAction(uid, ref component.MidaseToggleActionEntity, component.MidaseToggleAction, component: comp);
        
        Dirty(uid, component);
    }
    
    private void OnShutdown(EntityUid uid, MidaseUserComponent component, ComponentShutdown args)
    {
        Logger.DebugS("Midase", "MIDASE Trying to remove action");
        
        _action.RemoveAction(uid, component.MidaseToggleActionEntity);
    }
}