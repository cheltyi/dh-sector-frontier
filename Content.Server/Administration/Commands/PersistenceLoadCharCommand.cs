using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Frontier persistence (session-save): loads a single saved entity from the user-data directory and
/// teleports it to the caller's current location.
/// </summary>
[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceLoadCharCommand : LocalizedEntityCommands
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "persistenceloadchar";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var loadId = new ResPath(args[0]);
        var saveStat = _mapLoader.TryLoadEntity(loadId, out var entity);
        shell.WriteLine(Loc.GetString("cmd-persistenceload-result",
            ("status", saveStat), ("entity", entity?.ToString() ?? "null")));

        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        if (player.AttachedEntity == null)
        {
            shell.WriteLine("You must be attached to an entity to teleport the loaded one to yourself.");
            return;
        }

        var pe = player.AttachedEntity.Value;
        var coords = _entManager.GetComponent<TransformComponent>(pe).Coordinates;
        if (entity != null)
            _transform.SetCoordinates(entity.Value, coords);
    }
}
