using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Station;

/// <summary>
/// Similar to BecomesStation, but causes it to initialize on grid/map spawn instead of solely on gamemap load.
/// </summary>
[RegisterComponent]
[Access(typeof(GameTicker), typeof(StationSystem))]
public sealed partial class BecomesStationMidRoundComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)] public string? InitializedId = null;
    [DataField(required: true)] public string? Id = null;
    [DataField] public EntProtoId StationProto = new("StandardNanotrasenStation");
    [DataField] public string? EmergencyShuttleOverridePath = null;
    [DataField] public Dictionary<ProtoId<JobPrototype>, int>? AvailableJobs = null; // null = no jobs
}