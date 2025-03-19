using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Antags.Clockwork.UI;

public sealed class EnchantBoundUserInterface : BoundUserInterface
{
    private EnchantMenu? _menu;
    private EntityUid? _item;

    public EnchantBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
    
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
            SendPredictedMessage(new ClockworkEnchantMessage()
            {
                Item = base.EntMan.GetNetEntity(args.Item1),
                Action = args.Item2,
            });
        };
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