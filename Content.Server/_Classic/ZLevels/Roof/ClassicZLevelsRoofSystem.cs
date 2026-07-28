/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Classic.ZLevels.Roof;
using Robust.Shared.Map;

namespace Content.Server._Classic.ZLevels.Roof;

public sealed partial class ClassicZLevelsRoofSystem : ClassicSharedZLevelsRoofSystem
{
    [Dependency] private IMapManager _mapManager = default!;

    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        InitMaps();
        InitGrids();
    }
}
