/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Classic.ZLevels.Ghost;
using Content.Shared.Actions;

namespace Content.Server._Classic.ZLevels.Ghost;

public sealed partial class ClassicZLevelGhostMoverSystem : ClassicSharedZLevelGhostMoverSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClassicZLevelGhostMoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClassicZLevelGhostMoverComponent, ComponentRemove>(OnRemove);
    }

    private void OnMapInit(Entity<ClassicZLevelGhostMoverComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
    }

    private void OnRemove(Entity<ClassicZLevelGhostMoverComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
    }
}
