using FourPlayWebApp.Server.Services.Interfaces;
using System.Collections.Concurrent;

namespace FourPlayWebApp.Server.Services;

// Lightweight in-memory job observer for basic observability (last run times, status, message)
public class JobObserverService : IJobObserverService
{
    private readonly ConcurrentDictionary<string, JobRunInfo> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task RecordJobStartAsync(string jobName)
    {
        var info = _store.GetOrAdd(jobName, key => new JobRunInfo(key));
        var updated = info with { LastStartedUtc = DateTimeOffset.UtcNow, RunCount = info.RunCount + 1 };
        _store[jobName] = updated;
        return Task.CompletedTask;
    }

    public Task RecordJobSuccessAsync(string jobName, string? message = null)
    {
        var info = _store.GetOrAdd(jobName, key => new JobRunInfo(key));
        var updated = info with { LastSucceededUtc = DateTimeOffset.UtcNow, LastMessage = message };
        _store[jobName] = updated;
        return Task.CompletedTask;
    }

    public Task RecordJobFailureAsync(string jobName, string errorMessage)
    {
        var info = _store.GetOrAdd(jobName, key => new JobRunInfo(key));
        var updated = info with { LastFailedUtc = DateTimeOffset.UtcNow, LastMessage = errorMessage };
        _store[jobName] = updated;
        return Task.CompletedTask;
    }

    public Task<JobRunInfo?> GetJobInfoAsync(string jobName)
    {
        if (_store.TryGetValue(jobName, out var info)) return Task.FromResult<JobRunInfo?>(info);
        return Task.FromResult<JobRunInfo?>(null);
    }

    public Task<IEnumerable<JobRunInfo>> GetAllJobInfosAsync()
    {
        var copy = _store.Values.ToList();
        return Task.FromResult<IEnumerable<JobRunInfo>>(copy);
    }
}

