using Content.Server.DoAfter;
using Content.Shared.Buckle.Components;
using Content.Shared.Buckle;
using Content.Shared.DoAfter;
using Content.Shared.Starlight.Antags.Clockwork.Components;
using Content.Shared.Starlight.Antags.Clockwork.EntitySystems;
using Content.Shared._Starlight.Antags.Cults.Clockwork;
using Robust.Shared.GameObjects;

namespace Content.Server.Starlight.Antags.Clockwork.EntitySystems;

public sealed partial class AltarSystem : SharedAltarSystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<AltarComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<AltarComponent, AltarConvertationDoAfterEvent>(OnDoAfter);
    }
    
    private void OnStrapped(EntityUid uid, AltarComponent component, ref StrappedEvent args)
    {
        if (CanBeCultist(args.Buckle.Owner))
        {
            _appearance.SetData(uid, ClockworkAltarVisuals.Enabled, true);
            var @event = new AltarConvertationDoAfterEvent();
            var doAfter = new DoAfterArgs(EntityManager, args.Buckle.Owner, TimeSpan.FromSeconds(3), @event, uid)
            {
                BreakOnDamage = false,
                BreakOnDropItem = false,
                BreakOnHandChange = false,
                BreakOnMove = false,
                BreakOnWeightlessMove = false,
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
    }
    
    private void OnDoAfter(EntityUid uid, AltarComponent component, AltarConvertationDoAfterEvent args)
    {
        EnsureComp<ClockworkCultistComponent>(args.User);
        _buckle.Unbuckle(args.User, uid);
        _appearance.SetData(uid, ClockworkAltarVisuals.Enabled, false);
    }
}