using Content.Server.Parallax;

namespace Content.Server._Classic.Station;

/// <summary>
/// Bounds background generation of planetary terrain. Chunks containing viewers are
/// generated immediately; the rest of the view range is filled from nearest to farthest.
/// </summary>
[RegisterComponent, Access(typeof(BiomeSystem))]
public sealed partial class ClassicBiomeStreamingComponent : Component
{
    [DataField]
    public int BackgroundChunksPerTick = 2;

    [DataField]
    public TimeSpan UnloadDelay = TimeSpan.FromSeconds(1);

    /// <summary>Soft budget shared by background loads and unloads; a chunk is processed atomically.</summary>
    [DataField]
    public TimeSpan WorkBudget = TimeSpan.FromMilliseconds(4);

    public long WorkStarted;

    public readonly HashSet<Vector2i> ViewerChunks = new();
    public readonly List<(Vector2i Chunk, float Distance)> PendingLoads = new();
    public readonly Dictionary<Vector2i, TimeSpan> PendingUnloads = new();
}
