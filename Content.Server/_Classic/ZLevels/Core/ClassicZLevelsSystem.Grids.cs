/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared._Classic.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server._Classic.ZLevels.Core;

public sealed partial class ClassicZLevelsSystem
{
    [PublicAPI]
    public Entity<ClassicZGridNetworkComponent> CreateGridNetwork()
    {
        var ent  = Spawn();

        var comp = EnsureComp<ClassicZGridNetworkComponent>(ent);
        comp.NetworkId = Guid.NewGuid().ToString("N");
        Dirty(ent, comp);

        return (ent, comp);
    }

    [PublicAPI]
    public bool TryAddGridToNetwork(Entity<ClassicZGridNetworkComponent> gridNetwork, EntityUid grid)
    {
        if (!_mapGridQuery.HasComp(grid))
        {
            Log.Error($"ZGrid: {grid} is not a MapGrid.");
            return false;
        }

        if (TryGetGridNetwork(grid, out var existing))
        {
            Log.Error($"ZGrid: grid {grid} already in network {existing.Owner}.");
            return false;
        }

        gridNetwork.Comp.Grids.Add(grid);
        Dirty(gridNetwork);

        var zGridComp = EnsureComp<ClassicZGridComponent>(grid);
        zGridComp.NetworkId = gridNetwork.Comp.NetworkId;
        zGridComp.Network   = gridNetwork.Owner;
        Dirty(grid, zGridComp);

        var ev = new ClassicGridAddedIntoZNetworkEvent(gridNetwork);
        RaiseLocalEvent(grid, ref ev);

        RaiseLocalEvent(gridNetwork, new ClassicZLevelGridNetworkUpdatedEvent());

        return true;
    }

    [PublicAPI]
    public bool TryRemoveGridFromNetwork(EntityUid grid)
    {
        if (!TryGetGridNetwork(grid, out var gridNetwork))
            return false;

        gridNetwork.Comp.Grids.Remove(grid);
        RemComp<ClassicZGridComponent>(grid);

        if (!TerminatingOrDeleted(gridNetwork.Owner))
            Dirty(gridNetwork);

        var ev = new ClassicGridRemovedFromZNetworkEvent(gridNetwork);
        RaiseLocalEvent(grid, ref ev);

        if (gridNetwork.Comp.Grids.Count == 0 && !TerminatingOrDeleted(gridNetwork.Owner))
            QueueDel(gridNetwork);
        else
        {
            RaiseLocalEvent(gridNetwork, new ClassicZLevelGridNetworkUpdatedEvent());
        }

        return true;
    }

    /// <summary>
    /// Explicit teardown: removes every grid (raising <see cref="ClassicGridRemovedFromZNetworkEvent"/> per grid)
    /// and queues the manager for deletion.
    /// </summary>
    [PublicAPI]
    public void DeleteGridNetwork(Entity<ClassicZGridNetworkComponent> network)
    {
        // TryRemoveGridFromNetwork mutates Grids, so iterate a snapshot.
        foreach (var grid in network.Comp.Grids.ToList())
        {
            TryRemoveGridFromNetwork(grid);
        }

        if (!TerminatingOrDeleted(network.Owner))
            QueueDel(network);
    }
}
