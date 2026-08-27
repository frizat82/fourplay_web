using System.Runtime.ExceptionServices;
using FourPlayWebApp.Server.Services.Interfaces;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// frizat-703.2: shared by every job's catch block that wants both the existing admin
// job-monitor recording (IJobObserverService) AND the global JobFailureAlertListener/Discord
// alert to see a failure — the listener only fires when Quartz's JobWasExecuted receives a
// non-null jobException, so logging + recording alone (without rethrowing) silently opts a job
// out of alerting. ExceptionDispatchInfo preserves the original stack trace across this method
// boundary the same way a bare `throw;` would from inside the catch block itself.
internal static class JobFailureReporting
{
    internal static async Task RecordAndRethrowAsync(this IJobObserverService observer, string jobName, Exception ex)
    {
        Log.Error(ex, "{JobName} failed", jobName);
        await observer.RecordJobFailureAsync(jobName, ex.Message);
        ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
