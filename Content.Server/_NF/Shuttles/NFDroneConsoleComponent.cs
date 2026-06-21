namespace Content.Server.Shuttles.Components;

/// <summary>
/// Lets you remotely control a shuttle.
/// </summary>
[RegisterComponent]
public sealed partial class NFDroneConsoleComponent : Component
{
    [DataField(required: true)]
    public string Id = default!;

    /// <summary>
    /// <see cref="ShuttleConsoleComponent"/> that we're proxied into.
    /// </summary>
    // Dark Haven - persistence: not a [DataField]. Runtime cache of a remote console on another grid, recomputed
    // on UI open and cleared on close — serializing it dangles a cross-map reference on save.
    [ViewVariables]
    public EntityUid? Entity;
}
