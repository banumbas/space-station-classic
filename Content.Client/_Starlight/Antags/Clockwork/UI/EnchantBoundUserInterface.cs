using Content.Shared.Starlight.Antags.Clockwork.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Antags.Clockwork.UI;

public sealed class EnchantBoundUserInterface : BoundUserInterface
{
    private EnchantMenu? _menu;

    public EnchantBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        Open();
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<EnchantMenu>();
        _menu.Track(Owner);

        //_menu.OnAiRadial += args =>
        //{
        //    SendPredictedMessage(new ClockworkEnchantMessage()
        //    {
        //        Event = args,
        //    });
        //};
    }
}