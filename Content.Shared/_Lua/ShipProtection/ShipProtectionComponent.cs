using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom; // Dark Haven - persistence: TimeOffsetSerializer

namespace Content.Shared._Lua.ShipProtection;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ShipProtectionComponent : Component
{
    // Dark Haven - persistence: absolute CurTime, so use TimeOffsetSerializer + pause handling, or part
    // protection becomes permanent (or instantly drops) after a restart resets the clock.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ProtectionExpiresAt;
}

