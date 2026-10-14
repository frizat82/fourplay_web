using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodRulesToSeasonWeekConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPostSeason",
                table: "NflSeasonWeekConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredPicks",
                table: "NflSeasonWeekConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPostSeason",
                table: "CfbSeasonWeekConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredPicks",
                table: "CfbSeasonWeekConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // frizat-d47: backfill every existing row to the rule that was hardcoded until now
            // (GameHelpers.GetRequiredPicks / GetCfbRequiredPicks, NFL WeekId > 18, CFB slate >= 14 —
            // the /pick-rules tables). An unexpected week number maps to NULL, so the NOT NULL
            // column fails the migration loudly instead of guessing. CfbSlates stores no copy:
            // CfbRepository reads a slate's rules from its CfbSeasonWeekConfigs row.
            migrationBuilder.Sql("""
                UPDATE "NflSeasonWeekConfigs" SET
                    "IsPostSeason" = "WeekId" > 18,
                    "RequiredPicks" = CASE
                        WHEN "WeekId" BETWEEN 1 AND 18 THEN 4
                        WHEN "WeekId" IN (19, 20) THEN 3
                        WHEN "WeekId" = 21 THEN 2
                        WHEN "WeekId" = 22 THEN 1
                    END;
                """);
            migrationBuilder.Sql("""
                UPDATE "CfbSeasonWeekConfigs" SET
                    "IsPostSeason" = "IvLeagueWeekNumber" BETWEEN 14 AND 18,
                    "RequiredPicks" = CASE
                        WHEN "IvLeagueWeekNumber" = 99 THEN 0
                        WHEN "IvLeagueWeekNumber" BETWEEN 1 AND 14 THEN 4
                        WHEN "IvLeagueWeekNumber" IN (15, 16) THEN 3
                        WHEN "IvLeagueWeekNumber" = 17 THEN 2
                        WHEN "IvLeagueWeekNumber" = 18 THEN 1
                    END;
                """);
            // AddColumn needed a default to add NOT NULL columns to existing rows; once those are
            // backfilled, drop it, so a future season row that forgets these columns fails its
            // insert instead of silently getting 0 picks / regular season.
            migrationBuilder.Sql("""
                ALTER TABLE "NflSeasonWeekConfigs" ALTER COLUMN "IsPostSeason" DROP DEFAULT, ALTER COLUMN "RequiredPicks" DROP DEFAULT;
                ALTER TABLE "CfbSeasonWeekConfigs" ALTER COLUMN "IsPostSeason" DROP DEFAULT, ALTER COLUMN "RequiredPicks" DROP DEFAULT;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NflSeasonWeekConfigs_RequiredPicks",
                table: "NflSeasonWeekConfigs",
                sql: "\"RequiredPicks\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_RequiredPicks",
                table: "CfbSeasonWeekConfigs",
                sql: "\"RequiredPicks\" BETWEEN 1 AND 4 OR (\"IvLeagueWeekNumber\" = 99 AND \"RequiredPicks\" = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NflSeasonWeekConfigs_RequiredPicks",
                table: "NflSeasonWeekConfigs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_RequiredPicks",
                table: "CfbSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "IsPostSeason",
                table: "NflSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "RequiredPicks",
                table: "NflSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "IsPostSeason",
                table: "CfbSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "RequiredPicks",
                table: "CfbSeasonWeekConfigs");
        }
    }
}
