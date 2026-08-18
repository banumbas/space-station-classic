/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Classic.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared._Classic.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Classic.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class ClassicDeleteZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ClassicZLevelsSystem _zLevels = default!;

    public override string Command => "znetwork-delete";
    public override string Description => "Delete all maps into selected zNetwork + zNetwork entity";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<ClassicZMapNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp, out var meta))
        {
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        }
        return CompletionResult.FromHintOptions(options, "zNetwork net entity");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Wrong arguments count.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.HasComponent<ClassicZMapNetworkComponent>(target))
        {
            shell.WriteError($"Target entity doesnt have ClassicZLevelsNetworkComponent {args[0]}");
            return;
        }

        _zLevels.DeleteMapNetwork(target.Value);

        shell.WriteLine("ZNetwork and all its maps deleted.");
    }
}
