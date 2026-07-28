using System.Linq;
using Content.Shared._Classic.Vendors;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static System.StringComparison;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._Classic.Vendors;

public sealed class ClassicAutomatedVendorBui : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resource = default!;

    private ClassicAutomatedVendorWindow? _window;
    private ClassicAutomatedVendorBuiState? _lastState;

    public ClassicAutomatedVendorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ClassicAutomatedVendorWindow>();
        _window.Title = EntMan.GetComponentOrNull<MetaDataComponent>(Owner)?.EntityName ?? "Vendor";
        _window.ReagentsContainer.Visible = false;

        if (EntMan.TryGetComponent(Owner, out ClassicAutomatedVendorComponent? vendor))
            RebuildSections(vendor);

        _window.Search.OnTextChanged += OnSearchChanged;
        Refresh();
    }

    private void RebuildSections(ClassicAutomatedVendorComponent vendor)
    {
        if (_window == null)
            return;

        _window.Sections.DisposeAllChildren();

        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            var uiSection = new ClassicAutomatedVendorSection { Section = section };
            uiSection.Label.SetMessage(GetSectionName(section));

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                var uiEntry = new ClassicAutomatedVendorEntry();

                if (_prototype.TryIndex(entry.Id, out var entity))
                {
                    uiEntry.Texture.Textures = SpriteComponent.GetPrototypeTextures(entity, _resource)
                        .Select(o => o.Default)
                        .ToList();
                    if (entity.TryGetComponent<SpriteComponent>("Sprite", out var entitySprites) && entitySprites.AllLayers.Any())
                        uiEntry.Texture.Modulate = entitySprites.AllLayers.First().Color;

                    uiEntry.Panel.Button.Label.Text = entry.Name?.Replace("\\n", "\n") ?? entity.Name;

                    var name = entity.Name;
                    var color = ClassicAutomatedVendorPanel.DefaultColor;
                    var borderColor = ClassicAutomatedVendorPanel.DefaultBorderColor;
                    var hoverColor = ClassicAutomatedVendorPanel.DefaultBorderColor;

                    if (section.TakeAll != null || section.TakeOne != null)
                    {
                        name = $"Mandatory: {name}";
                        color = Color.FromHex("#251A0C");
                        borderColor = Color.FromHex("#805300");
                        hoverColor = Color.FromHex("#805300");
                    }
                    else if (entry.Recommended)
                    {
                        uiEntry.Panel.Button.Label.Text = $"★ {uiEntry.Panel.Button.Label.Text}";
                        name = $"Recommended: {name}";
                        color = Color.FromHex("#102919");
                        borderColor = Color.FromHex("#3A9B52");
                        hoverColor = Color.FromHex("#3A9B52");
                    }

                    uiEntry.Panel.Color = color;
                    uiEntry.Panel.BorderColor = borderColor;
                    uiEntry.Panel.HoveredColor = hoverColor;

                    var msg = new FormattedMessage();
                    msg.AddText(name);
                    msg.PushNewline();

                    if (!string.IsNullOrWhiteSpace(entity.Description))
                        msg.AddText(entity.Description);

                    var tooltip = new Tooltip();
                    tooltip.SetMessage(msg);

                    uiEntry.TooltipLabel.ToolTip = entity.Description;
                    uiEntry.TooltipLabel.TooltipDelay = 0;
                    uiEntry.TooltipLabel.TooltipSupplier = _ => tooltip;

                    var sectionI = sectionIndex;
                    var entryI = entryIndex;
                    uiEntry.Panel.Button.OnPressed += _ => OnButtonPressed(sectionI, entryI);
                }

                uiSection.Entries.AddChild(uiEntry);
            }

            _window.Sections.AddChild(uiSection);
        }
    }

    private void OnButtonPressed(int sectionIndex, int entryIndex)
    {
        var msg = new ClassicVendorVendBuiMsg(sectionIndex, entryIndex);
        SendPredictedMessage(msg);
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        ApplySearchFilter(args.Text);
    }

    private void ApplySearchFilter(string? text)
    {
        if (_window == null)
            return;

        foreach (var sectionControl in _window.Sections.Children)
        {
            if (sectionControl is not ClassicAutomatedVendorSection section)
                continue;

            var any = false;
            foreach (var entriesControl in section.Entries.Children)
            {
                if (entriesControl is not ClassicAutomatedVendorEntry entry)
                    continue;

                if (string.IsNullOrWhiteSpace(text))
                    entry.Visible = true;
                else
                    entry.Visible = entry.Panel.Button.Label.Text?.Contains(text, OrdinalIgnoreCase) ?? false;

                if (entry.Visible)
                    any = true;
            }

            section.Visible = any;
        }
    }

    public void Refresh()
    {
        if (_window == null || _lastState == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out ClassicAutomatedVendorComponent? vendor))
            return;

        var anyEntryWithPoints = false;
        var userPoints = vendor.PointsType == null
            ? _lastState.Points
            : _lastState.ExtraPoints?.GetValueOrDefault(vendor.PointsType) ?? 0;

        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            
            if (sectionIndex >= _window.Sections.ChildCount)
                continue;

            var uiSection = (ClassicAutomatedVendorSection) _window.Sections.GetChild(sectionIndex);
            
            var sectionDisabled = false;
            if (section.Choices is { } choices)
            {
                if (_lastState.Choices.GetValueOrDefault(choices.Id) >= choices.Amount)
                {
                    sectionDisabled = true;
                }
            }

            var anyAmount = false;
            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                
                if (entryIndex >= uiSection.Entries.ChildCount)
                    continue;
                    
                var uiEntry = (ClassicAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                var disabled = sectionDisabled || (entry.Amount.HasValue && entry.Amount <= 0);
                
                if (section.TakeAll is { } takeAllId)
                {
                    if (_lastState.TakeAll.Contains((takeAllId, entry.Id.Id)))
                        disabled = true;
                }
                if (section.TakeOne is { } takeOneId)
                {
                    if (_lastState.TakeOne.Contains(takeOneId))
                        disabled = true;
                }

                if (entry.Points != null)
                {
                    anyEntryWithPoints = true;
                    uiEntry.Amount.Text = $"{entry.Points}P";

                    if (userPoints < entry.Points)
                        disabled = true;
                }
                else
                {
                    uiEntry.Amount.Text = entry.Amount?.ToString() ?? "∞";
                }

                uiEntry.Amount.Modulate = disabled ? Color.Red : Color.White;
                uiEntry.Panel.Button.Disabled = disabled;

                if (!string.IsNullOrWhiteSpace(uiEntry.Amount.Text))
                    anyAmount = true;
            }

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                if (entryIndex >= uiSection.Entries.ChildCount)
                    continue;
                var uiEntry = (ClassicAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                uiEntry.Amount.Visible = anyAmount;
            }
        }

        ApplySearchFilter(_window.Search.Text);
        _window.PointsLabel.Text = anyEntryWithPoints ? $"Points Remaining: {userPoints}" : string.Empty;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is ClassicVendorRefreshBuiMsg)
        {
            Refresh();
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        
        if (state is ClassicAutomatedVendorBuiState vendorState)
        {
            _lastState = vendorState;
            Refresh();
        }
    }

    private FormattedMessage GetSectionName(ClassicVendorSection section)
    {
        var name = new FormattedMessage();
        name.PushTag(new MarkupNode("bold", new MarkupParameter(section.Name.ToUpperInvariant()), null));
        name.AddText(section.Name.ToUpperInvariant());
        
        if (section.TakeAll != null)
            name.AddText(" (TAKE ALL)");
        else if (section.TakeOne != null)
            name.AddText(" (TAKE ONE)");
        else if (section.Choices is { } choices)
        {
            var left = choices.Amount - (_lastState?.Choices.GetValueOrDefault(choices.Id) ?? 0);
            if (left > 0)
                name.AddText($" (CHOOSE {left})");
        }

        name.Pop();
        return name;
    }
}
