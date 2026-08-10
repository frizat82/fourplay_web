using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RequireSpreadLockDatetime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing NULL SpreadLockDatetime rows before the column becomes NOT NULL.
            // NflSeasonWeekConfigs has none as of this migration — CfbSeasonWeekConfigs has 4:
            //
            // 1. Season 2025, EspnWeekNumber 18 (IvLeagueWeekNumber 16, "FBS Playoff" —
            //    CFP Quarterfinals) is a real, in-scope week that was simply missing its lock
            //    time. Best-effort backfill matching the sibling First Round week's pattern
            //    (Season 2025 IV=15: locks 6 days before WeekStartDate, 10:00 UTC) — not verified
            //    against the original source spreadsheet, but this week is already complete
            //    (2025 season), so nothing live depends on its exact value going forward.
            migrationBuilder.Sql(
                """
                UPDATE "CfbSeasonWeekConfigs"
                SET "SpreadLockDatetime" = "WeekStartDate" - INTERVAL '6 days' + INTERVAL '10 hours'
                WHERE "Season" = 2025 AND "EspnWeekNumber" = 18 AND "SpreadLockDatetime" IS NULL;
                """);

            // 2-4. The remaining nulls are all IvLeagueWeekNumber=99 sentinel/excluded rows
            // (Season 2026, EspnWeekNumber 0/17/19 — a placeholder pre-season marker and two
            // "Dead" bye weeks). These are never scheduled (CfbSpreadScheduleSource filters out
            // InScopeIvLeague=false and IvLeagueWeekNumber=99), so the exact value is functionally
            // irrelevant — backfilled to their own WeekStartDate at midnight UTC purely to satisfy
            // the new NOT NULL constraint.
            migrationBuilder.Sql(
                """
                UPDATE "CfbSeasonWeekConfigs"
                SET "SpreadLockDatetime" = "WeekStartDate"::timestamptz
                WHERE "IvLeagueWeekNumber" = 99 AND "SpreadLockDatetime" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "NflSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "CfbSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "NflSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SpreadLockDatetime",
                table: "CfbSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
