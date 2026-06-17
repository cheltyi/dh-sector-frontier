using Content.Server.Persistence.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Frontier persistence (session-save): saves a grid entity to a file, dumping players off it and
/// deleting it afterward.
/// </summary>
[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceSaveGridCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly PersistenceSystem _persistence = default!;

    public override string Command => "persistencesavegrid";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Not enough arguments.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet) || !_ent.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }

        if (_persistence.SaveGrid(uid.Value, new ResPath(args[1]), out var errorMessage, dumpSpecialEntities: true, deleteGrid: true))
        {
            shell.WriteLine("Save successful. Look in the user data directory.");
        }
        else
        {
            shell.WriteError("Save unsuccessful!");
            if (!string.IsNullOrWhiteSpace(errorMessage))
                shell.WriteError(errorMessage);
        }
    }
}
