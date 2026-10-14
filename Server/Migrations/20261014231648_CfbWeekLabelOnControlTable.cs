using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class CfbWeekLabelOnControlTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeekLabel",
                table: "CfbSeasonWeekConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            // frizat-d47: each CFB round's name lives on its control-table row, like NFL's
            // WeekLabel. Backfilled to exactly what CfbWeekLabelHelper derived until now (an
            // excluded week (99) is named by its Notes; any other number falls back to
            // "Week N"), then the default is dropped and a check keeps it non-blank.
            migrationBuilder.Sql("""
                UPDATE "CfbSeasonWeekConfigs" SET "WeekLabel" = CASE
                    WHEN "IvLeagueWeekNumber" = 99 THEN COALESCE(NULLIF("Notes", ''), 'Excluded')
                    WHEN "IvLeagueWeekNumber" = 14 THEN 'Conf. Championships'
                    WHEN "IvLeagueWeekNumber" = 15 THEN 'CFP First Round'
                    WHEN "IvLeagueWeekNumber" = 16 THEN 'CFP Quarterfinals'
                    WHEN "IvLeagueWeekNumber" = 17 THEN 'CFP Semifinals'
                    WHEN "IvLeagueWeekNumber" = 18 THEN 'CFP Championship'
                    ELSE 'Week ' || "IvLeagueWeekNumber"
                END;
                ALTER TABLE "CfbSeasonWeekConfigs" ALTER COLUMN "WeekLabel" DROP DEFAULT;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_WeekLabel",
                table: "CfbSeasonWeekConfigs",
                sql: "\"WeekLabel\" <> ''");

            // CfbSlates kept a copy of the label (and a SlateType nothing read) made at seeding
            // time; CfbRepository now reads the label live from the row.
            migrationBuilder.DropColumn(
                name: "Label",
                table: "CfbSlates");

            migrationBuilder.DropColumn(
                name: "SlateType",
                table: "CfbSlates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "CfbSlates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SlateType",
                table: "CfbSlates",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Restore the slates' label copy from their rows, so a rolled-back build (which reads
            // CfbSlates.Label) doesn't show blank round names.
            migrationBuilder.Sql("""
                UPDATE "CfbSlates" s SET "Label" = c."WeekLabel"
                FROM "CfbSeasonWeekConfigs" c
                WHERE c."Season" = s."Season" AND c."IvLeagueWeekNumber" = s."SlateNumber";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_WeekLabel",
                table: "CfbSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "WeekLabel",
                table: "CfbSeasonWeekConfigs");
        }
    }
}
