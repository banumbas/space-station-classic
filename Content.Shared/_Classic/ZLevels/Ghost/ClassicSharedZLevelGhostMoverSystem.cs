/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */


using Content.Shared._Classic.ZLevels.Core.EntitySystems;

namespace Content.Shared._Classic.ZLevels.Ghost;

public abstract partial class ClassicSharedZLevelGhostMoverSystem : EntitySystem
{
    [Dependency] private ClassicSharedZLevelsSystem _zLevel = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicZLevelGhostMoverComponent, ClassicZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<ClassicZLevelGhostMoverComponent, ClassicZLevelActionDown>(OnZLevelDown);
    }

    private void OnZLevelDown(Entity<ClassicZLevelGhostMoverComponent> ent, ref ClassicZLevelActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveDown(ent);
    }

    private void OnZLevelUp(Entity<ClassicZLevelGhostMoverComponent> ent, ref ClassicZLevelActionUp args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveUp(ent);
    }
}
