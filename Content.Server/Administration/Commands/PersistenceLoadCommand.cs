using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Frontier persistence (session-save): loads a full map file from the user-data directory into the world.
/// </summary>
[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceLoadCommand : LocalizedEntityCommands
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override string Command => "persistenceload";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var loadId = new ResPath(args[0]);
        var saveStat = _mapLoader.TryLoadMap(loadId, out var entity, out _);
        shell.WriteLine(Loc.GetString("cmd-persistenceload-result",
            ("status", saveStat), ("entity", entity?.ToString() ?? "null")));
    }
}
