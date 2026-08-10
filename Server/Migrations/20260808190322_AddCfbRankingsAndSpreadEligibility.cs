using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfbRankingsAndSpreadEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLeagueEligible",
                table: "CfbSpreads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: every pre-existing CfbSpreads row was already inserted under the OLD
            // ingestion-time ranked-team filter (see CfbLiveScoreFetcher pre-frizat-9m0), so it was
            // already league-eligible by that rule. Without this, they'd silently vanish from
            // GetSpreads/GetScores the moment the new serving-layer filter goes live.
            migrationBuilder.Sql("UPDATE \"CfbSpreads\" SET \"IsLeagueEligible\" = true;");

            migrationBuilder.CreateTable(
                name: "CfbRankings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    EspnWeekNumber = table.Column<int>(type: "integer", nullable: false),
                    EspnEventId = table.Column<int>(type: "integer", nullable: false),
                    TeamAbbreviation = table.Column<string>(type: "text", nullable: false),
                    CuratedRank = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CfbRankings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_EspnEventId_TeamAbbreviat~",
                table: "CfbRankings",
                columns: new[] { "Season", "EspnWeekNumber", "EspnEventId", "TeamAbbreviation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CfbRankings");

            migrationBuilder.DropColumn(
                name: "IsLeagueEligible",
                table: "CfbSpreads");
        }
    }
}
