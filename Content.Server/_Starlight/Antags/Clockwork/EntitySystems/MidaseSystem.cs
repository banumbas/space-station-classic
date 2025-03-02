using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Stacks;
using Content.Shared.Hands;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class MidaseSystem : SharedMidaseSystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<MidaseTransformableComponent, GotEquippedHandEvent>(OnEquipHand);

        base.Initialize();
    }
    
    private void OnEquipHand(EntityUid uid, MidaseTransformableComponent component, GotEquippedHandEvent args)
    {
        if (TryComp<MidaseUserComponent>(args.User, out var midaceUser) && midaceUser.MidaseEnabled && component.TargetEntity != null)
        {
            if (component.TransformStack && TryComp<StackComponent>(uid, out var stack))
                for (int i = stack.Count; i > 0; i--)
                {
                    var item = Spawn(component.TargetEntity.Value, Transform(args.User).Coordinates);
                    _stack.TryMergeToContacts(item);
                }
            else
                Spawn(component.TargetEntity.Value, Transform(uid).Coordinates);
            QueueDel(uid);
        }
    }
}