using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Frontier persistence (session-save): saves a single entity (e.g. a character) to a file in the
/// user-data directory.
/// </summary>
[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceSaveCharCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override string Command => "persistencesavechar";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var entNet) || !_entManager.TryGetEntity(entNet, out var entId))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }

        var saveFilePath = (args.Length > 1 ? args[1] : null) ?? _config.GetCVar(CCVars.GameMap);
        if (string.IsNullOrWhiteSpace(saveFilePath))
        {
            shell.WriteError(Loc.GetString("cmd-persistencesave-no-path", ("cvar", nameof(CCVars.GameMap))));
            return;
        }

        var saveStat = _mapLoader.TrySaveGeneric(entId.Value, new ResPath(saveFilePath), out _);
        shell.WriteLine(Loc.GetString("cmd-savemap-success") + $" {saveStat}");
    }
}
