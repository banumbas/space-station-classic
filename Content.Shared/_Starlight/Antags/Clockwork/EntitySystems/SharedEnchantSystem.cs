using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Starlight.Antags.Clockwork.EntitySystems;

public abstract class SharedEnchantSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<EnchantableComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<EnchantableComponent, MapInitEvent>(OnMapInit);
        base.Initialize();
    }
    
    private void OnMapInit(EntityUid uid, EnchantableComponent component, MapInitEvent args)
    {
        _actionContainer.EnsureAction(uid, ref component.EnchantActionEntity, component.EnchantAction);
        Dirty(uid, component);
    }
    
    private void OnGetActions(EntityUid uid, EnchantableComponent component, GetItemActionsEvent args)
    {
        if (HasComp<EnchantUserComponent>(args.User))
            args.AddAction(component.EnchantActionEntity);
    }
}