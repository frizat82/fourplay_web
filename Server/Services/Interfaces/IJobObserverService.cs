using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IJobObserverService
{
    Task RecordJobStartAsync(string jobName);
    Task RecordJobSuccessAsync(string jobName, string? message = null);
    Task RecordJobFailureAsync(string jobName, string errorMessage);
    Task<JobRunInfo?> GetJobInfoAsync(string jobName);
    Task<IEnumerable<JobRunInfo>> GetAllJobInfosAsync();
}

public record JobRunInfo(string JobName)
{
    public DateTimeOffset? LastStartedUtc { get; init; }
    public DateTimeOffset? LastSucceededUtc { get; init; }
    public DateTimeOffset? LastFailedUtc { get; init; }
    public string? LastMessage { get; init; }
    public int RunCount { get; init; }
};
