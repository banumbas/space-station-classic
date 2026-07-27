using Content.Client._Classic.ZCollapse.Overlays;
using Content.Shared._Classic.ZCollapse.Events;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Classic.ZCollapse;

public sealed partial class ClassicZCollapseClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public Dictionary<NetEntity, Dictionary<Vector2i, int>>? Grids;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ClassicZCollapseOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<ClassicZCollapseOverlaySnapshotEvent>(OnSnapshotUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<ClassicZCollapseDebugOverlay>();
    }

    private void OnOverlayToggled(ClassicZCollapseOverlayToggledEvent ev)
    {
        if (ev.IsEnabled)
            _overlayMan.AddOverlay(new ClassicZCollapseDebugOverlay());
        else
        {
            _overlayMan.RemoveOverlay<ClassicZCollapseDebugOverlay>();
            Grids = null;
        }
    }

    private void OnSnapshotUpdate(ClassicZCollapseOverlaySnapshotEvent ev)
    {
        Grids ??= new Dictionary<NetEntity, Dictionary<Vector2i, int>>();

        foreach (var (grid, tiles) in ev.Grids)
        {
            Grids[grid] = tiles;
        }
    }
}


