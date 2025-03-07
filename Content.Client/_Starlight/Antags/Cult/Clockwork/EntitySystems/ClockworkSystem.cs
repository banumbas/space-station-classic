using Content.Shared._Starlight.Antags.Cults.Clockwork;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Antags.Cult.Clockwork.EntitySystems;

public sealed class ClockworkSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClockworkCultistComponent, GetStatusIconsEvent>(GetCultistIcon);
        SubscribeLocalEvent<ClockworkMasterComponent, GetStatusIconsEvent>(GetMasterIcon);
    }

    private void GetCultistIcon(Entity<ClockworkCultistComponent> ent, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }

    private void GetMasterIcon(Entity<ClockworkMasterComponent> ent, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }
}