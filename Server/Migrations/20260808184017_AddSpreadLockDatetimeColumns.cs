using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSpreadLockDatetimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "NflSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstEligibleGameDate",
                table: "CfbSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "CfbSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill from "NFL and CFB Week Control Table (2).xlsx" — all datetime values converted
            // from the sheet's Eastern wall-clock times to true UTC (accounting for EDT/EST per date).
            // This also corrects the pre-existing bug where WeekStartDatetime/WeekEndDatetime/
            // FirstGameOfWeekStartDatetime were seeded by tagging raw ET numbers as DateTimeKind.Utc
            // without converting (see 20260727164222_AddNflSeasonWeekConfig.cs).
            //
            // 2025 season Week 17/18/Wild Card Weekend: the source sheet's WEEK_START/END_DATETIME
            // boundaries for these three rows don't reflect real game dates (Week 18's stated boundary
            // opens AFTER its own games; Week 18 and Wild Card share an identical boundary window) —
            // confirmed intentional per the sheet's own notes ("nominal" boundary distinct from actual
            // game days), but that creates ambiguous current-week resolution for NflCurrentWeekService.
            // Corrected here to non-overlapping ranges anchored to the real 2025 NFL schedule (verified
            // via nfl.com/espn.com): Week 18 games were Sat Jan 3 / Sun Jan 4 2026; Wild Card Weekend
            // began Sat Jan 10 2026. The 2026 season's equivalent rows were already clean in the sheet.
            migrationBuilder.UpdateData(
                table: "NflSeasonWeekConfigs",
                keyColumns: new[] { "Season", "WeekId" },
                keyValues: new object[,] {
                    { 2026, 1 }, { 2026, 2 }, { 2026, 3 }, { 2026, 4 }, { 2026, 5 }, { 2026, 6 },
                    { 2026, 7 }, { 2026, 8 }, { 2026, 9 }, { 2026, 10 }, { 2026, 11 }, { 2026, 12 },
                    { 2026, 13 }, { 2026, 14 }, { 2026, 15 }, { 2026, 16 }, { 2026, 17 }, { 2026, 18 },
                    { 2026, 19 }, { 2026, 20 }, { 2026, 21 }, { 2026, 22 },
                    { 2025, 1 }, { 2025, 2 }, { 2025, 3 }, { 2025, 4 }, { 2025, 5 }, { 2025, 6 },
                    { 2025, 7 }, { 2025, 8 }, { 2025, 9 }, { 2025, 10 }, { 2025, 11 }, { 2025, 12 },
                    { 2025, 13 }, { 2025, 14 }, { 2025, 15 }, { 2025, 16 }, { 2025, 17 }, { 2025, 18 },
                    { 2025, 19 }, { 2025, 20 }, { 2025, 21 }, { 2025, 22 },
                },
                columns: new[] { "WeekStartDatetime", "WeekEndDatetime", "FirstGameOfWeekStartDatetime", "SpreadLockDatetime" },
                values: new object[,] {
                    { new DateTime(2026, 9, 8, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 15, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 9, 10, 0, 20, 0, DateTimeKind.Utc), new DateTime(2026, 9, 9, 14, 20, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 15, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 22, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 9, 18, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 9, 17, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 22, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 29, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 9, 25, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 9, 24, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 29, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 10, 2, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 6, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 13, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 10, 9, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 13, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 20, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 10, 16, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 27, 3, 59, 59, DateTimeKind.Utc), new DateTime(2026, 10, 23, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 10, 22, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 27, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 3, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 10, 30, 0, 15, 0, DateTimeKind.Utc), new DateTime(2026, 10, 29, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 10, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 11, 6, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 11, 5, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 10, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 17, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 11, 13, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 11, 12, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 17, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 24, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 11, 20, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 11, 19, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 24, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 11, 26, 1, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 1, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 8, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 12, 4, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 12, 3, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 8, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 15, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 12, 11, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 12, 10, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 15, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 22, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 12, 18, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 12, 17, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 22, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 29, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 12, 25, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 25, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 29, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 5, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 1, 2, 1, 15, 0, DateTimeKind.Utc), new DateTime(2027, 1, 1, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2027, 1, 5, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 12, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 1, 9, 18, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 9, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2027, 1, 13, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 20, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 1, 16, 18, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 16, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2027, 1, 20, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 27, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 1, 23, 18, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 23, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2027, 1, 27, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 2, 3, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 1, 31, 20, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 31, 9, 59, 59, DateTimeKind.Utc) },
                    { new DateTime(2027, 2, 10, 5, 0, 0, DateTimeKind.Utc), new DateTime(2027, 2, 16, 4, 59, 59, DateTimeKind.Utc), new DateTime(2027, 2, 14, 23, 30, 0, DateTimeKind.Utc), new DateTime(2027, 2, 14, 13, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 2, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 9, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 9, 5, 0, 20, 0, DateTimeKind.Utc), new DateTime(2025, 9, 4, 14, 20, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 9, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 16, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 9, 12, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 9, 11, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 16, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 23, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 9, 19, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 9, 18, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 23, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 30, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 9, 26, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 9, 25, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 30, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 7, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 10, 3, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 10, 2, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 7, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 14, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 10, 10, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 10, 9, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 14, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 21, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 10, 17, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 10, 16, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 21, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 28, 3, 59, 59, DateTimeKind.Utc), new DateTime(2025, 10, 24, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 10, 23, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 28, 4, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 4, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 10, 31, 0, 15, 0, DateTimeKind.Utc), new DateTime(2025, 10, 30, 14, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 4, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 11, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 11, 7, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 11, 6, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 11, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 18, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 11, 14, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 11, 13, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 18, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 25, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 11, 21, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 11, 20, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 2, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 9, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 12, 5, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 12, 4, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 9, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 16, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 12, 12, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 12, 11, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 16, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 23, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 12, 19, 1, 15, 0, DateTimeKind.Utc), new DateTime(2025, 12, 18, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 23, 5, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 30, 4, 59, 59, DateTimeKind.Utc), new DateTime(2025, 12, 25, 18, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 25, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 30, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 3, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 2, 1, 15, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 15, 14, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 3, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 3, 21, 30, 0, DateTimeKind.Utc), new DateTime(2026, 1, 3, 11, 29, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 10, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 13, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 10, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 13, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 20, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 17, 21, 30, 0, DateTimeKind.Utc), new DateTime(2026, 1, 17, 11, 29, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 20, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 27, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 25, 20, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 25, 9, 59, 59, DateTimeKind.Utc) },
                    { new DateTime(2026, 2, 3, 5, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 10, 4, 59, 59, DateTimeKind.Utc), new DateTime(2026, 2, 8, 23, 30, 0, DateTimeKind.Utc), new DateTime(2026, 2, 8, 13, 30, 0, DateTimeKind.Utc) },
                });

            // Backfill CFB from the same control table — FirstEligibleGameDate/SpreadLockDatetime only
            // (WeekStartDate/WeekEndDate are DateOnly, seeded separately, not part of this bug).
            migrationBuilder.UpdateData(
                table: "CfbSeasonWeekConfigs",
                keyColumns: new[] { "Season", "EspnWeekNumber" },
                keyValues: new object[,] {
                    { 2026, 0 }, { 2026, 1 }, { 2026, 2 }, { 2026, 3 }, { 2026, 4 }, { 2026, 5 },
                    { 2026, 6 }, { 2026, 7 }, { 2026, 8 }, { 2026, 9 }, { 2026, 10 }, { 2026, 11 },
                    { 2026, 12 }, { 2026, 13 }, { 2026, 14 }, { 2026, 15 }, { 2026, 16 }, { 2026, 17 },
                    { 2026, 18 }, { 2026, 19 }, { 2026, 20 }, { 2026, 21 },
                    { 2025, 0 }, { 2025, 1 }, { 2025, 2 }, { 2025, 3 }, { 2025, 4 }, { 2025, 5 },
                    { 2025, 6 }, { 2025, 7 }, { 2025, 8 }, { 2025, 9 }, { 2025, 10 }, { 2025, 11 },
                    { 2025, 12 }, { 2025, 13 }, { 2025, 14 }, { 2025, 15 }, { 2025, 16 }, { 2025, 17 },
                    { 2025, 18 }, { 2025, 19 }, { 2025, 20 }, { 2025, 21 },
                },
                columns: new[] { "FirstEligibleGameDate", "SpreadLockDatetime" },
                values: new object[,] {
                    { null, null },
                    { new DateTime(2026, 9, 3, 22, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 10, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 10, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 17, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 17, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 9, 24, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 24, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 1, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 8, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 15, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 22, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 22, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 10, 29, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 29, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 19, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 11, 26, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 26, 7, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 4, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 12, 20, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 12, 10, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 12, 18, 19, 30, 0, DateTimeKind.Utc), new DateTime(2026, 12, 18, 9, 30, 0, DateTimeKind.Utc) },
                    { null, null },
                    { new DateTime(2026, 12, 31, 0, 30, 0, DateTimeKind.Utc), new DateTime(2026, 12, 30, 14, 30, 0, DateTimeKind.Utc) },
                    { null, null },
                    { new DateTime(2027, 1, 15, 0, 30, 0, DateTimeKind.Utc), new DateTime(2027, 1, 14, 14, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2027, 1, 26, 0, 30, 0, DateTimeKind.Utc), new DateTime(2027, 1, 25, 14, 30, 0, DateTimeKind.Utc) },
                    { null, null },
                    { new DateTime(2025, 8, 28, 21, 30, 0, DateTimeKind.Utc), new DateTime(2025, 8, 28, 11, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 5, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 5, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 11, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 11, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 18, 23, 30, 0, DateTimeKind.Utc), new DateTime(2025, 9, 18, 13, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 9, 25, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 9, 25, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 2, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 2, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 9, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 9, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 16, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 16, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 23, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 23, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 10, 30, 23, 0, 0, DateTimeKind.Utc), new DateTime(2025, 10, 30, 13, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 7, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 6, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 13, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 20, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 11, 27, 17, 0, 0, DateTimeKind.Utc), new DateTime(2025, 11, 27, 7, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 6, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 5, 14, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 13, 20, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 13, 10, 0, 0, DateTimeKind.Utc) },
                    { new DateTime(2025, 12, 19, 19, 30, 0, DateTimeKind.Utc), new DateTime(2025, 12, 19, 9, 30, 0, DateTimeKind.Utc) },
                    { null, null },
                    { new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 14, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 9, 0, 30, 0, DateTimeKind.Utc), new DateTime(2026, 1, 8, 14, 30, 0, DateTimeKind.Utc) },
                    { new DateTime(2026, 1, 20, 0, 30, 0, DateTimeKind.Utc), new DateTime(2026, 1, 19, 14, 30, 0, DateTimeKind.Utc) },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpreadLockDatetime",
                table: "NflSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "FirstEligibleGameDate",
                table: "CfbSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "SpreadLockDatetime",
                table: "CfbSeasonWeekConfigs");
        }
    }
}
