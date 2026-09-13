using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Classic.Chemistry.Components;

/// <summary>
/// Universal component that enforces a whitelist of reagents allowed to be transferred into this container.
/// Any solution transfer containing unauthorized reagents will be cancelled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReagentWhitelistComponent : Component
{
    /// <summary>
    /// Set of permitted reagent prototype IDs.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public HashSet<ProtoId<ReagentPrototype>> Whitelist = new();

    /// <summary>
    /// Popup message shown when an unauthorized chemical transfer is attempted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId DeniedPopup = "reagent-whitelist-denied";
}
