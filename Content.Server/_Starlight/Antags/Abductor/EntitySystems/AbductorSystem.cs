using Content.Server.Actions;
using Content.Server.DoAfter;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Content.Shared.Tag;
using Robust.Server.Containers;
using Content.Shared._Starlight.Computers.RemoteEye;

namespace Content.Server.Starlight.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    public override void Initialize()
    {
        InitializeActions();
        InitializeGizmo();
        InitializeConsole();
        InitializeOrgans();
        InitializeVest();
        InitializeExtractor();
        base.Initialize();
    }
}
