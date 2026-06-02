using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public enum StepInvalidReason
{
    None,
    NeedsOperatingTable,
    Armor,
    MissingTool,
    DisabledTool,
    TooHigh,
    NotEnoughReagent,
    InvalidMode,
}

// From 0 to 100: 100 it's 100% chance to success, 0 it's 0% chance to success.
public enum StepDifficulty : int
{
    Easy = 90,
    Medium = 60,
    Hard = 30,
    VeryHard = 10,
}

#region UI

[Serializable, NetSerializable]
public enum SurgeryUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState : BoundUserInterfaceState
{
    public required Dictionary<NetEntity, List<(EntProtoId, string, bool)>> Choices { get; init; }
}

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg : BoundUserInterfaceMessage
{
    public required NetEntity Part { get; init; }
    public required EntProtoId Surgery { get; init; }
    public required EntProtoId Step { get; init; }
}

#endregion
