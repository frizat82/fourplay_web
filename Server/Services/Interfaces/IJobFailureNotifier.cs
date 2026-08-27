namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IJobFailureNotifier
{
    Task NotifyAsync(string jobName, string triggerName, string errorMessage, CancellationToken cancellationToken = default);
}
