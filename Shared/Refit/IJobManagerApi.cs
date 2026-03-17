using FourPlayWebApp.Shared.Models;
using Refit;

namespace FourPlayWebApp.Shared.Refit {
    public interface IJobManagerApi {
        [Post("/api/JobManager/run-spreads")]
        Task<ApiResponse<string>> RunSpreads();
        [Post("/api/JobManager/run-missing")]
        Task<ApiResponse<string>> RunMissing();

        [Post("/api/JobManager/run-scores")]
        Task<ApiResponse<string>> RunScores();

        [Post("/api/JobManager/run-users")]
        Task<ApiResponse<string>> RunUserJob();

        [Get("/api/jobManager/get-jobs")]
        Task<IEnumerable<JobStatusResponse>> GetAllJobsStatusAsync();

        [Get("/api/jobManager/get-job/{jobName}")]
        Task<JobStatusResponse?> GetJobStatusAsync(string jobName);

        [Delete("/api/jobManager/delete-job/{jobName}")]
        Task<ApiResponse<bool>> DeleteJob(string jobName);

        [Get("/api/jobManager/get-next-spread-job")]
        Task<DateTimeOffset?> GetNextSpreadJobAsync();
    }
}