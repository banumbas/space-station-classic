using Content.Shared.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared._Classic.PMC;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class ShowPMCIconsSystem : EquipmentHudSystem<ShowPMCIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<FactionIconPrototype> FactionIconPMCAssault = "FactionIconPMCAssault";
    private static readonly ProtoId<FactionIconPrototype> FactionIconPMCMedic = "FactionIconPMCMedic";
    private static readonly ProtoId<FactionIconPrototype> FactionIconPMCSpecialist = "FactionIconPMCSpecialist";
    private static readonly ProtoId<FactionIconPrototype> FactionIconPMCCommander = "FactionIconPMCCommander";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PMCAssaultComponent, GetStatusIconsEvent>(OnGetPMCAssaultStatusIconsEvent);
        SubscribeLocalEvent<PMCMedicComponent, GetStatusIconsEvent>(OnGetPMCMedicStatusIconsEvent);
        SubscribeLocalEvent<PMCSpecialistComponent, GetStatusIconsEvent>(OnGetPMCSpecialistStatusIconsEvent);
        SubscribeLocalEvent<PMCCommanderComponent, GetStatusIconsEvent>(OnGetPMCCommanderStatusIconsEvent);
    }

    private void OnGetPMCAssaultStatusIconsEvent(EntityUid uid, PMCAssaultComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex(FactionIconPMCAssault, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }

    private void OnGetPMCMedicStatusIconsEvent(EntityUid uid, PMCMedicComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex(FactionIconPMCMedic, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }

    private void OnGetPMCSpecialistStatusIconsEvent(EntityUid uid, PMCSpecialistComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex(FactionIconPMCSpecialist, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }

    private void OnGetPMCCommanderStatusIconsEvent(EntityUid uid, PMCCommanderComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_prototype.TryIndex(FactionIconPMCCommander, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}
