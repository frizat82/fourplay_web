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
            // WeekLabel. Backfilled to exactly what CfbWeekLabelHelper derived until now; an
            // excluded week (99) is named by its Notes. Then the default goes, so a future row
            // without a label fails its insert rather than showing a blank round name.
            migrationBuilder.Sql("""
                UPDATE "CfbSeasonWeekConfigs" SET "WeekLabel" = CASE
                    WHEN "IvLeagueWeekNumber" = 99 THEN COALESCE(NULLIF("Notes", ''), 'Excluded')
                    WHEN "IvLeagueWeekNumber" BETWEEN 1 AND 13 THEN 'Week ' || "IvLeagueWeekNumber"
                    WHEN "IvLeagueWeekNumber" = 14 THEN 'Conf. Championships'
                    WHEN "IvLeagueWeekNumber" = 15 THEN 'CFP First Round'
                    WHEN "IvLeagueWeekNumber" = 16 THEN 'CFP Quarterfinals'
                    WHEN "IvLeagueWeekNumber" = 17 THEN 'CFP Semifinals'
                    WHEN "IvLeagueWeekNumber" = 18 THEN 'CFP Championship'
                END;
                ALTER TABLE "CfbSeasonWeekConfigs" ALTER COLUMN "WeekLabel" DROP DEFAULT;
                """);

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
            migrationBuilder.DropColumn(
                name: "WeekLabel",
                table: "CfbSeasonWeekConfigs");

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
        }
    }
}
