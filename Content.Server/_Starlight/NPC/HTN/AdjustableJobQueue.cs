using Robust.Shared.CPUJob.JobQueues.Queues;

namespace Content.Server._Starlight.NPC.HTN;

public sealed class AdjustableJobQueue(double maxTime) : JobQueue
{
    private double _maxTime = maxTime;

    public override double MaxTime => _maxTime;

    public void SetMaxTime(double maxTime)
        => _maxTime = maxTime;
}
