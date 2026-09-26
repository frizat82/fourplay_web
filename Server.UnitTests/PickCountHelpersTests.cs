using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>frizat-xbq: one submitted-pick count shared by the NFL and CFB count endpoints.</summary>
public class PickCountHelpersTests
{
    [Fact]
    public void CountByUser_CountsEachUsersPicks()
    {
        var counts = PickCountHelpers.CountByUser(["alice", "alice", "alice", "bob"]);

        Assert.Equal(3, counts.Single(c => c.UserId == "alice").PickCount);
        Assert.Equal(1, counts.Single(c => c.UserId == "bob").PickCount);
    }

    [Fact]
    public void CountByUser_ReturnsEmptyListForNoPicks()
    {
        Assert.Empty(PickCountHelpers.CountByUser([]));
    }

    /// <summary>
    /// The whole point of a separate endpoint (rather than turning off the kickoff-hiding on the
    /// picks endpoints for owners) is that a league owner is also a PLAYER — they must never learn
    /// which teams a member picked before kickoff. Guards against someone adding Team/PickType
    /// to this DTO later.
    /// </summary>
    [Fact]
    public void MemberPickCountDto_CarriesCountsOnly_NeverWhichTeamWasPicked()
    {
        var properties = typeof(MemberPickCountDto).GetProperties().Select(p => p.Name).OrderBy(n => n);

        Assert.Equal(["PickCount", "UserId"], properties);
    }
}
