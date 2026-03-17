using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;

namespace FourPlayWebApp.Server.UnitTests;

public class JobObserverServiceTests
{
    // ── RecordJobStartAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RecordJobStartAsync_StoresJobName()
    {
        var sut = new JobObserverService();

        await sut.RecordJobStartAsync("MyJob");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.NotNull(info);
        Assert.Equal("MyJob", info.JobName);
    }

    [Fact]
    public async Task RecordJobStartAsync_SetsLastStartedUtc()
    {
        var sut = new JobObserverService();
        var before = DateTimeOffset.UtcNow;

        await sut.RecordJobStartAsync("MyJob");

        var after = DateTimeOffset.UtcNow;
        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.NotNull(info?.LastStartedUtc);
        Assert.True(info!.LastStartedUtc >= before);
        Assert.True(info.LastStartedUtc <= after);
    }

    [Fact]
    public async Task RecordJobStartAsync_IncrementsRunCount()
    {
        var sut = new JobObserverService();

        await sut.RecordJobStartAsync("MyJob");
        await sut.RecordJobStartAsync("MyJob");
        await sut.RecordJobStartAsync("MyJob");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Equal(3, info!.RunCount);
    }

    [Fact]
    public async Task RecordJobStartAsync_DoesNotSetSucceededOrFailed()
    {
        var sut = new JobObserverService();

        await sut.RecordJobStartAsync("MyJob");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Null(info!.LastSucceededUtc);
        Assert.Null(info.LastFailedUtc);
    }

    // ── RecordJobSuccessAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordJobSuccessAsync_SetsLastSucceededUtc()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("MyJob");
        var before = DateTimeOffset.UtcNow;

        await sut.RecordJobSuccessAsync("MyJob");

        var after = DateTimeOffset.UtcNow;
        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.NotNull(info?.LastSucceededUtc);
        Assert.True(info!.LastSucceededUtc >= before);
        Assert.True(info.LastSucceededUtc <= after);
    }

    [Fact]
    public async Task RecordJobSuccessAsync_StoresOptionalMessage()
    {
        var sut = new JobObserverService();

        await sut.RecordJobSuccessAsync("MyJob", "All good");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Equal("All good", info!.LastMessage);
    }

    [Fact]
    public async Task RecordJobSuccessAsync_NullMessage_WhenNotProvided()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("MyJob");

        await sut.RecordJobSuccessAsync("MyJob");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Null(info!.LastMessage);
    }

    [Fact]
    public async Task RecordJobSuccessAsync_DoesNotSetLastFailedUtc()
    {
        var sut = new JobObserverService();

        await sut.RecordJobSuccessAsync("MyJob");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Null(info!.LastFailedUtc);
    }

    // ── RecordJobFailureAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordJobFailureAsync_SetsLastFailedUtc()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("MyJob");
        var before = DateTimeOffset.UtcNow;

        await sut.RecordJobFailureAsync("MyJob", "Something went wrong");

        var after = DateTimeOffset.UtcNow;
        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.NotNull(info?.LastFailedUtc);
        Assert.True(info!.LastFailedUtc >= before);
        Assert.True(info.LastFailedUtc <= after);
    }

    [Fact]
    public async Task RecordJobFailureAsync_StoresErrorMessage()
    {
        var sut = new JobObserverService();

        await sut.RecordJobFailureAsync("MyJob", "Timeout exceeded");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Equal("Timeout exceeded", info!.LastMessage);
    }

    [Fact]
    public async Task RecordJobFailureAsync_DoesNotSetLastSucceededUtc()
    {
        var sut = new JobObserverService();

        await sut.RecordJobFailureAsync("MyJob", "error");

        var info = await sut.GetJobInfoAsync("MyJob");
        Assert.Null(info!.LastSucceededUtc);
    }

    // ── GetAllJobInfosAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllJobInfosAsync_ReturnsEmptyCollection_WhenNoJobsRecorded()
    {
        var sut = new JobObserverService();

        var all = await sut.GetAllJobInfosAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllJobInfosAsync_ReturnsAllDistinctJobs()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("JobA");
        await sut.RecordJobStartAsync("JobB");
        await sut.RecordJobStartAsync("JobC");

        var all = (await sut.GetAllJobInfosAsync()).ToList();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, j => j.JobName == "JobA");
        Assert.Contains(all, j => j.JobName == "JobB");
        Assert.Contains(all, j => j.JobName == "JobC");
    }

    [Fact]
    public async Task GetAllJobInfosAsync_ReturnsCopy_NotLiveReference()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("JobA");

        var snapshot1 = (await sut.GetAllJobInfosAsync()).ToList();
        await sut.RecordJobStartAsync("JobB");
        var snapshot2 = (await sut.GetAllJobInfosAsync()).ToList();

        Assert.Single(snapshot1);
        Assert.Equal(2, snapshot2.Count);
    }

    // ── GetJobInfoAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetJobInfoAsync_ReturnsNull_ForUnknownJob()
    {
        var sut = new JobObserverService();

        var info = await sut.GetJobInfoAsync("DoesNotExist");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetJobInfoAsync_ReturnsCorrectJob_ByName()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("Alpha");
        await sut.RecordJobStartAsync("Beta");

        var info = await sut.GetJobInfoAsync("Beta");

        Assert.NotNull(info);
        Assert.Equal("Beta", info!.JobName);
    }

    [Fact]
    public async Task GetJobInfoAsync_IsCaseInsensitive()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("myjob");

        var info = await sut.GetJobInfoAsync("MYJOB");

        Assert.NotNull(info);
        Assert.Equal("myjob", info!.JobName);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordJobSuccessAsync_ForNeverStartedJob_CreatesEntry()
    {
        // success recorded without a prior start is valid (GetOrAdd creates the entry)
        var sut = new JobObserverService();

        await sut.RecordJobSuccessAsync("GhostJob", "ok");

        var info = await sut.GetJobInfoAsync("GhostJob");
        Assert.NotNull(info);
        Assert.Equal("GhostJob", info!.JobName);
        Assert.NotNull(info.LastSucceededUtc);
        Assert.Equal("ok", info.LastMessage);
        // RunCount was never incremented via RecordJobStartAsync
        Assert.Equal(0, info.RunCount);
    }

    [Fact]
    public async Task RecordJobFailureAsync_ForNeverStartedJob_CreatesEntry()
    {
        var sut = new JobObserverService();

        await sut.RecordJobFailureAsync("GhostJob", "crash on first run");

        var info = await sut.GetJobInfoAsync("GhostJob");
        Assert.NotNull(info);
        Assert.NotNull(info!.LastFailedUtc);
        Assert.Equal("crash on first run", info.LastMessage);
        Assert.Equal(0, info.RunCount);
    }

    [Fact]
    public async Task DuplicateJobName_OverwritesPreviousState()
    {
        var sut = new JobObserverService();
        await sut.RecordJobStartAsync("Duplicate");
        await sut.RecordJobSuccessAsync("Duplicate", "first run ok");

        // Second lifecycle
        await sut.RecordJobStartAsync("Duplicate");
        await sut.RecordJobFailureAsync("Duplicate", "second run failed");

        var all = (await sut.GetAllJobInfosAsync()).ToList();
        // Still only one entry for this job
        Assert.Single(all);

        var info = await sut.GetJobInfoAsync("Duplicate");
        Assert.Equal("second run failed", info!.LastMessage);
        Assert.NotNull(info.LastFailedUtc);
        Assert.Equal(2, info.RunCount);
    }

    [Fact]
    public async Task MultipleJobs_AreStoredAndRetrievedIndependently()
    {
        var sut = new JobObserverService();

        await sut.RecordJobStartAsync("Job1");
        await sut.RecordJobSuccessAsync("Job1", "job1 done");
        await sut.RecordJobStartAsync("Job2");
        await sut.RecordJobFailureAsync("Job2", "job2 error");

        var job1 = await sut.GetJobInfoAsync("Job1");
        var job2 = await sut.GetJobInfoAsync("Job2");

        Assert.Equal("job1 done", job1!.LastMessage);
        Assert.NotNull(job1.LastSucceededUtc);
        Assert.Null(job1.LastFailedUtc);

        Assert.Equal("job2 error", job2!.LastMessage);
        Assert.NotNull(job2.LastFailedUtc);
        Assert.Null(job2.LastSucceededUtc);
    }
}
