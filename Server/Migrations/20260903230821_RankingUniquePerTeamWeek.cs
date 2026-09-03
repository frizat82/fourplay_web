using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RankingUniquePerTeamWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CfbRanking used to be append-only (one row per capture, including ESPN's 99
            // "unranked" sentinel). Before the new unique index below can be created, prod data
            // must be cleaned up: drop the unranked noise, then dedupe down to one row per
            // (Season, EspnWeekNumber, TeamAbbreviation) — keeping the most recently captured row.
            migrationBuilder.Sql(@"
                DELETE FROM ""CfbRankings"" WHERE ""CuratedRank"" NOT BETWEEN 1 AND 25;

                DELETE FROM ""CfbRankings"" a
                USING ""CfbRankings"" b
                WHERE a.""Season"" = b.""Season""
                  AND a.""EspnWeekNumber"" = b.""EspnWeekNumber""
                  AND a.""TeamAbbreviation"" = b.""TeamAbbreviation""
                  AND (a.""CapturedAtUtc"" < b.""CapturedAtUtc""
                       OR (a.""CapturedAtUtc"" = b.""CapturedAtUtc"" AND a.""Id"" < b.""Id""));
            ");

            migrationBuilder.DropIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_EspnEventId_TeamAbbreviat~",
                table: "CfbRankings");

            migrationBuilder.CreateIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_TeamAbbreviation",
                table: "CfbRankings",
                columns: new[] { "Season", "EspnWeekNumber", "TeamAbbreviation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_TeamAbbreviation",
                table: "CfbRankings");

            migrationBuilder.CreateIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_EspnEventId_TeamAbbreviat~",
                table: "CfbRankings",
                columns: new[] { "Season", "EspnWeekNumber", "EspnEventId", "TeamAbbreviation" });
        }
    }
}
