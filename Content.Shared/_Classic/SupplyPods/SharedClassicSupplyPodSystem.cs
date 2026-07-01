using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Classic.SupplyPods;

/// <summary>
/// Abstract base for the supply pod delivery system. Provides the public API
/// that both server and client can reference. Server-side implementations
/// perform actual entity spawning/delivery; client-side handles animations.
/// </summary>
public abstract class SharedClassicSupplyPodSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedTransformSystem Transform = default!;

    /// <summary>
    /// Default prototype for a supply pod entity. This entity should be an
    /// EntityStorage with a <see cref="ClassicSupplyPodComponent"/>.
    /// </summary>
    public static readonly EntProtoId DefaultPodPrototype = "ClassicSupplyPod";

    /// <summary>
    /// Maps a visual variant to its falling-animation RSI path and state id.
    /// Centralized so server + client stay in sync without hardcoding strings.
    /// </summary>
    public static readonly Dictionary<ClassicSupplyPodVisual, (string Rsi, string State)> FallingSprites = new()
    {
        { ClassicSupplyPodVisual.Default, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "default_falling_animation") },
        //{ ClassicSupplyPodVisual.Nanotrasen, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "nanotrasen_falling_animation") },
        //{ ClassicSupplyPodVisual.Syndicate, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "syndicate_falling_animation") },
        //{ ClassicSupplyPodVisual.Bluespace, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "bluespace_falling_animation") },
        //{ ClassicSupplyPodVisual.Cult, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "cult_falling_animation") },
        //{ ClassicSupplyPodVisual.Gondola, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "gondola_falling_animation") },
        //{ ClassicSupplyPodVisual.Honk, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "honk_falling_animation") },
        //{ ClassicSupplyPodVisual.Orange, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "orange_falling_animation") },
        //{ ClassicSupplyPodVisual.Squad, ("/Textures/_Classic/Effects/supplypod_falling.rsi", "squad_falling_animation") },
    };
}
