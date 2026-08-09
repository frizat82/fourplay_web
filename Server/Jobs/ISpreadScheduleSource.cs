namespace FourPlayWebApp.Server.Jobs;

// Sport-specific candidate source plugged into SpreadSchedulerJobBase — NFL and CFB each read
// their own SeasonWeekConfig table and compute HasData from their own spread table, but the
// scheduling logic that consumes the result is 100% shared (frizat CLAUDE.md: siblings, not
// separate products).
public interface ISpreadScheduleSource {
    Task<IEnumerable<SpreadTriggerCandidate>> GetCandidatesAsync();
}
