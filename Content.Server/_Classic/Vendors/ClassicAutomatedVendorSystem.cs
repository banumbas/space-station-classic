using Content.Shared._Classic.Vendors;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Classic.Vendors;

public sealed class ClassicAutomatedVendorSystem : SharedClassicAutomatedVendorSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<ClassicAutomatedVendorComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<ClassicAutomatedVendorComponent, ClassicVendorVendBuiMsg>(OnVendMessage);
    }

    private void OnUIOpened(Entity<ClassicAutomatedVendorComponent> vendor, ref BoundUIOpenedEvent args)
    {
        UpdateUIState(vendor.Owner, args.Actor);
    }

    private void OnVendMessage(Entity<ClassicAutomatedVendorComponent> vendor, ref ClassicVendorVendBuiMsg args)
    {
        var user = args.Actor;

        if (!TryComp<ClassicVendorUserComponent>(user, out var userComp))
            return;

        if (args.Section < 0 || args.Section >= vendor.Comp.Sections.Count)
            return;

        var section = vendor.Comp.Sections[args.Section];
        
        if (args.Entry < 0 || args.Entry >= section.Entries.Count)
            return;

        var entry = section.Entries[args.Entry];
        
        // Point validation
        var userPoints = vendor.Comp.PointsType == null
            ? userComp.Points
            : userComp.ExtraPoints?.GetValueOrDefault(vendor.Comp.PointsType) ?? 0;

        if (entry.Points != null && userPoints < entry.Points)
            return; // Not enough points

        // Validation for limits (TakeAll, TakeOne, Choices) could be added here
        
        // Deduct points
        if (entry.Points != null)
        {
            if (vendor.Comp.PointsType == null)
                userComp.Points -= entry.Points.Value;
            else
            {
                userComp.ExtraPoints ??= new();
                userComp.ExtraPoints[vendor.Comp.PointsType] = userPoints - entry.Points.Value;
            }
        }

        // Spawn items
        for (int i = 0; i < entry.Spawn; i++)
        {
            var spawned = Spawn(entry.Id, Transform(vendor.Owner).Coordinates);
            _hands.TryPickupAnyHand(user, spawned);
        }

        if (vendor.Comp.Sound != null)
            _audio.PlayPvs(vendor.Comp.Sound, vendor.Owner);

        UpdateUIState(vendor.Owner, user);
    }

    private void UpdateUIState(EntityUid vendor, EntityUid user)
    {
        if (!TryComp<ClassicVendorUserComponent>(user, out var userComp))
            return;

        var state = new ClassicAutomatedVendorBuiState(
            userComp.Points,
            userComp.ExtraPoints,
            userComp.Choices,
            userComp.TakeAll,
            userComp.TakeOne
        );

        _ui.SetUiState(vendor, ClassicAutomatedVendorUI.Key, state);
    }
}
