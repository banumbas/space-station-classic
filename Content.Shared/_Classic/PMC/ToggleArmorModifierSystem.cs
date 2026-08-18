using Content.Shared.Damage;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Armor;

namespace Content.Shared._Classic.PMC;

public sealed class ToggleArmorModifierSystem : EntitySystem
{
    [Dependency] private readonly SharedArmorSystem _armor = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleArmorModifierComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleArmorModifierComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnMapInit(EntityUid uid, ToggleArmorModifierComponent component, MapInitEvent args)
    {
        if (TryComp<ArmorComponent>(uid, out var armor))
        {
            component.BaseDamageModifiers = armor.Modifiers;
        }
    }

    private void OnToggled(EntityUid uid, ToggleArmorModifierComponent component, ref ItemToggledEvent args)
    {
        if (TryComp<ArmorComponent>(uid, out var armor))
        {
            if (args.Activated)
            {
                if (component.BaseDamageModifiers == null)
                    component.BaseDamageModifiers = armor.Modifiers;
                _armor.SetModifiers(uid, component.ActiveDamageModifiers, armor);
            }
            else
            {
                if (component.BaseDamageModifiers != null)
                {
                    _armor.SetModifiers(uid, component.BaseDamageModifiers, armor);
                }
            }
        }
    }
}
