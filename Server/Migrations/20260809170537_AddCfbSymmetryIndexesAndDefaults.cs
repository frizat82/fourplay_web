using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfbSymmetryIndexesAndDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbSlates",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbScores",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbPicks",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_CfbSpreads_CfbSlateId",
                table: "CfbSpreads",
                column: "CfbSlateId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbSlates_Season_SlateNumber",
                table: "CfbSlates",
                columns: new[] { "Season", "SlateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "IvLeagueWeekNumber" },
                unique: true,
                filter: "\"IvLeagueWeekNumber\" <> 99");

            migrationBuilder.CreateIndex(
                name: "IX_CfbScores_CfbSlateId",
                table: "CfbScores",
                column: "CfbSlateId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbScores_EspnEventId",
                table: "CfbScores",
                column: "EspnEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CfbPicks_CfbSlateId",
                table: "CfbPicks",
                column: "CfbSlateId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbPicks_LeagueId",
                table: "CfbPicks",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbPicks_UserId_LeagueId_CfbSlateId_Season_Team_PickType",
                table: "CfbPicks",
                columns: new[] { "UserId", "LeagueId", "CfbSlateId", "Season", "Team", "PickType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CfbSpreads_CfbSlateId",
                table: "CfbSpreads");

            migrationBuilder.DropIndex(
                name: "IX_CfbSlates_Season_SlateNumber",
                table: "CfbSlates");

            migrationBuilder.DropIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs");

            migrationBuilder.DropIndex(
                name: "IX_CfbScores_CfbSlateId",
                table: "CfbScores");

            migrationBuilder.DropIndex(
                name: "IX_CfbScores_EspnEventId",
                table: "CfbScores");

            migrationBuilder.DropIndex(
                name: "IX_CfbPicks_CfbSlateId",
                table: "CfbPicks");

            migrationBuilder.DropIndex(
                name: "IX_CfbPicks_LeagueId",
                table: "CfbPicks");

            migrationBuilder.DropIndex(
                name: "IX_CfbPicks_UserId_LeagueId_CfbSlateId_Season_Team_PickType",
                table: "CfbPicks");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbSlates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbScores",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DateCreated",
                table: "CfbPicks",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamptz",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
