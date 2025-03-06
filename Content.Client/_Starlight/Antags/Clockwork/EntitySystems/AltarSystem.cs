using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Client.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class AltarSystem : SharedMidaseSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AltarComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }
    
    private void OnAppearanceChanged(EntityUid uid, AltarComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.Sprite.TryGetLayer((int) ClockworkAltarVisualLayers.Base, out var baseLayer))
            return;
        
        var state = component.BaseState;
        if (_appearance.TryGetData<bool>(uid, ClockworkAltarVisuals.Enabled, out var enabled, args.Component))
        {
            if (enabled)
                state = component.FastState;
        
            if (args.Sprite.LayerMapTryGet(ClockworkAltarVisualLayers.Glow, out var glowId) && args.Sprite.TryGetLayer(glowId, out var glowLayer))
                glowLayer.Visible = enabled;
        }
        
        baseLayer.SetState(state);
    }
}