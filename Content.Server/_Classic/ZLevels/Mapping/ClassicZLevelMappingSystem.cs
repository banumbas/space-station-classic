/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Classic.ZLevels.Core;
using Content.Shared._Classic.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Classic.ZLevels.Mapping;

public sealed partial class ClassicZLevelMappingSystem : EntitySystem
{
    [Dependency] private ClassicZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicZMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClassicZMapComponent, ClassicMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<ClassicZMapComponent> ent, ref ClassicMapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
        else
        {
            var hasInitializedMaps = false;
            foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
            {
                if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
                {
                    hasInitializedMaps = true;
                    break;
                }
            }

            if (hasInitializedMaps)
                _map.InitializeMap(ent.Owner);
        }
    }

    private void OnMapInit(Entity<ClassicZMapComponent> ent, ref MapInitEvent args)
    {
        if (!_zLevels.TryGetMapNetwork(ent, out var network))
            return;

        EntityManager.AddComponents(ent, network.Comp.Components);
    }
}
