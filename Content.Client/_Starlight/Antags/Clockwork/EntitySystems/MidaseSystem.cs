using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class MidaseSystem : SharedMidaseSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<MidaseUserComponent, AppearanceChangeEvent>(OnAppearanceChange);

        base.Initialize();
    }
    
    private void OnAppearanceChange(EntityUid uid, MidaseUserComponent component, ref AppearanceChangeEvent args)
    {
        if (!Resolve(uid, ref args.Sprite))
            return;
        
        if (_appearance.TryGetData<bool>(uid, MidaseVisuals.Enabled, out var enabled, args.Component) && enabled && component.MidaseVisuals != null)
            component.LayerId = args.Sprite.AddLayer(component.MidaseVisuals);
        else if (component.LayerId != 0)
            args.Sprite.RemoveLayer(component.LayerId);
    }
}