namespace FourPlayWebApp.Server.Jobs;

/// <summary>
/// One run at a time per job class, across every job key that runs it. Quartz's
/// [DisallowConcurrentExecution] only stops one job KEY overlapping itself, but the scores jobs
/// are registered under several keys — one per fixed time, plus the "... Scores On Final" key
/// FinalScoresWatcher triggers the moment a game ends — and two runs saving the same finals at
/// once race on the scores tables' unique index. In-process is enough: the API runs as a single
/// instance with Quartz's in-memory store.
/// </summary>
internal static class JobRunGate<TJob>
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RunAsync(Func<Task> run, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await run();
        }
        finally
        {
            Gate.Release();
        }
    }
}
