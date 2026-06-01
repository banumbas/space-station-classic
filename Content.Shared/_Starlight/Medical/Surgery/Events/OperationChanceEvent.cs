namespace Content.Shared.Starlight.Medical.Surgery.Events;

/// <summary>
///    Raised to determine the chance of success for an operation.
/// </summary>
[ByRefEvent]
public record struct OperationChanceEvent(EntityUid Performer, EntityUid Target, EntityUid? Tool, float Chance = 1f, string Reason = "", bool ForceSuccess = false);
