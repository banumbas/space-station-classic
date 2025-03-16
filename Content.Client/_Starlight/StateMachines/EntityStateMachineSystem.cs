using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Client.Materials;
using Content.Shared._Starlight.StateMachines;
using Content.Shared.Materials;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.StateMachines;

public sealed class EntityStateMachineSystem : SharedEntityStateMachineSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStateMachineComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<EntityStateMachineComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<EntityStateMachine>(ent.Owner, EntityStateMachineApperance.State, out var state, args.Component))
            return;

        foreach (var item in Enum.GetValues<EntityStateMachine>())
        {
            if (!args.Sprite.LayerMapTryGet(item, out var layer))
                continue;
            args.Sprite.LayerSetAnimationTime(layer, 0f);
            args.Sprite.LayerSetVisible(layer, item == state);
        }
    }
}