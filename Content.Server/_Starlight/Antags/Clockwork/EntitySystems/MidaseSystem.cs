using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Stacks;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class MidaseSystem : SharedMidaseSystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<MidaseTransformableComponent, GotEquippedHandEvent>(OnEquipHand);
        SubscribeLocalEvent<MidaseUserComponent, MidaseToggleEvent>(OnMidaseToggle);
    }
    
    // <summary>
    // Activating when midase user activated action, trying to get an item in hand and then transform it, 
    // if hand is empty, they just enabling midase hand(transform any transformable item what pickuped, enchant any item what pickuped).
    // </summary>
    private void OnMidaseToggle(EntityUid uid, MidaseUserComponent component, ref MidaseToggleEvent args)
    {
        if (_hands.TryGetActiveItem(uid, out var item))
        {
            if (TryComp<MidaseTransformableComponent>(item, out var transformable))
            {
                TransformItem(item.Value, transformable, out var transformedItem);
                _hands.TryPickupAnyHand(uid, transformedItem, true, false, false);
            }
            //else if (TryComp<Enchantable>) TODO: enchanting
        }
        component.MidaseEnabled = !component.MidaseEnabled;
        
        //_action.SetToggled(component.MidaseToggleActionEntity, component.MidaseEnabled);
        
        //if (TryComp<AppearanceComponent>(uid, out var appearance) && !_net.IsClient)
        //    _appearance.SetData(uid, MidaseVisuals.Enabled, component.MidaseEnabled, appearance);
    }
    
    private void TransformItem(EntityUid uid, MidaseTransformableComponent component, out EntityUid item)
    {
        item = default;
        //if (component.TransformStack && TryComp<StackComponent>(uid, out var stack))
        //    for (int i = stack.Count; i > 0; i--)
        //    {
        //        item = Spawn(component.TargetEntity.Value, Transform(args.User).Coordinates);
        //        _stack.TryMergeToContacts(item);
        //    }
        //else
        //    item = Spawn(component.TargetEntity.Value, Transform(uid).Coordinates);
        //QueueDel(uid);
    }
    
    private void OnEquipHand(EntityUid uid, MidaseTransformableComponent component, GotEquippedHandEvent args)
    {
        if (TryComp<MidaseUserComponent>(args.User, out var midaceUser) && midaceUser.MidaseEnabled && component.TargetEntity != null)
        {
            TransformItem(uid, component, out var transformedItem);
            _hands.TryPickupAnyHand(args.User, transformedItem, true, false, false);
        }
    }
}