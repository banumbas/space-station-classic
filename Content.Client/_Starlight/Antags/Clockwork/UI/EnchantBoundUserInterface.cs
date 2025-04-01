using Content.Shared.Starlight.Antags.Clockwork.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Antags.Clockwork.UI;

[UsedImplicitly]
public sealed class EnchantBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private EnchantMenu? _menu;
    private EntityUid? _item;
    
    public void ToggleWindow()
    {
        if (base.IsOpened)
            Close();
        else
            Open();
    }

    protected override void Open()
    {
        base.Open();
        
        _menu = this.CreateWindow<EnchantMenu>();
        _menu.Track(Owner);
        _menu.BuildItemButtons();
        _menu.UpdatePosition();

        _menu.OnItemSelected += args =>
        {
            _item = args;
            _menu.BuildEnchantButtons(args);
        };
        
        _menu.OnEnchantSelected += args => 
        {
            SendMessage(new ClockworkEnchantMessage(base.EntMan.GetNetEntity(args.Item1), args.Item2));
        };
        
        _menu.OnClose += Close;
    }
    
    public override void Update()
    {
        if (_menu == null)
        {
            base.Close();
            return;
        }
        
        if (_item == null)
            _menu.BuildItemButtons();
        else
            _menu.BuildEnchantButtons(_item.Value);
        _menu.UpdatePosition();
    }
}