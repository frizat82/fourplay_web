using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <summary>
    /// frizat-b4g: CFB goes down to one control table, like NFL.
    /// - CfbSlates is dropped: a slate IS an in-scope CfbSeasonWeekConfigs row. Every CfbSlateId
    ///   (CfbSpreads/CfbScores/CfbPicks, and CFB TeamLiveNotificationStates.Period) is re-pointed
    ///   from the slate's id to its control-table row's id, matched on Season + slate number.
    /// - CfbRankings are keyed by our week (the slate number), not ESPN's week number.
    /// - InScopeIvLeague is the one skip flag: a skipped CFB week has no week number (was 99);
    ///   NFL rows get the same flag (all in scope).
    /// - WeekType/ScoringFormat become the shared enums' names on both tables; ScoringFormat is
    ///   checked against RequiredPicks, and IsPostSeason against WeekType.
    /// - The ESPN week number column on the CFB control table is dropped.
    /// Every mapping CASE has no ELSE: an unexpected value becomes NULL on a NOT NULL column and
    /// fails the migration loudly instead of guessing.
    /// </summary>
    public partial class OneCfbControlTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_CfbPicks_CfbSlates_CfbSlateId", table: "CfbPicks");
            migrationBuilder.DropForeignKey(name: "FK_CfbScores_CfbSlates_CfbSlateId", table: "CfbScores");
            migrationBuilder.DropForeignKey(name: "FK_CfbSpreads_CfbSlates_CfbSlateId", table: "CfbSpreads");

            // The old schema had two skip markers (InScopeIvLeague false, IvLeagueWeekNumber 99).
            // They agree on every real row; refuse to guess if one doesn't.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "CfbSeasonWeekConfigs" WHERE "InScopeIvLeague" = ("IvLeagueWeekNumber" = 99)) THEN
                        RAISE EXCEPTION 'frizat-b4g: a CFB control-table row''s InScopeIvLeague disagrees with its 99 week number';
                    END IF;
                END $$;
                """);

            // Re-point every slate reference from CfbSlates.Id to the matching in-scope
            // control-table row's Id. Two passes via negative ids: old and new id ranges overlap,
            // and the (CfbSlateId, HomeTeam)-style unique indexes are checked row by row, so a
            // direct UPDATE could transiently collide.
            var references = new[] {
                ("CfbSpreads", "CfbSlateId", ""),
                ("CfbScores", "CfbSlateId", ""),
                ("CfbPicks", "CfbSlateId", ""),
                ("TeamLiveNotificationStates", "Period", "AND t.\"Sport\" = 1"),
            };
            foreach (var (table, column, scope) in references)
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" t SET "{column}" = -c."Id"
                    FROM "CfbSlates" s
                    JOIN "CfbSeasonWeekConfigs" c ON c."Season" = s."Season" AND c."IvLeagueWeekNumber" = s."SlateNumber" AND c."InScopeIvLeague"
                    WHERE t."{column}" = s."Id" {scope};
                    """);
            }
            // Anything still positive matched no in-scope row: it would now point at an unrelated
            // row (or a skipped week nothing reads). Fail loudly before flipping the rest back.
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF {string.Join(" OR ", references.Select(r => $"""EXISTS (SELECT 1 FROM "{r.Item1}" t WHERE t."{r.Item2}" > 0 {r.Item3})"""))} THEN
                        RAISE EXCEPTION 'frizat-b4g: a CFB spread/score/pick/notification state did not map to an in-scope control-table row';
                    END IF;
                END $$;
                """);
            foreach (var (table, column, scope) in references)
                migrationBuilder.Sql($"""UPDATE "{table}" t SET "{column}" = -t."{column}" WHERE t."{column}" < 0 {scope};""");

            // AP rankings: ESPN week -> our week (the row's slate number), before ESPN's week
            // column goes. Same two-pass negative trick for the (Season, week, team) unique index.
            migrationBuilder.RenameColumn(name: "EspnWeekNumber", table: "CfbRankings", newName: "WeekNumber");
            migrationBuilder.RenameIndex(
                name: "IX_CfbRankings_Season_EspnWeekNumber_TeamAbbreviation",
                table: "CfbRankings",
                newName: "IX_CfbRankings_Season_WeekNumber_TeamAbbreviation");
            migrationBuilder.Sql("""
                UPDATE "CfbRankings" r SET "WeekNumber" = -c."IvLeagueWeekNumber"
                FROM "CfbSeasonWeekConfigs" c
                WHERE c."Season" = r."Season" AND c."EspnWeekNumber" = r."WeekNumber" AND c."InScopeIvLeague";
                DELETE FROM "CfbRankings" WHERE "WeekNumber" >= 0; -- captured for no slate: nothing reads it
                UPDATE "CfbRankings" SET "WeekNumber" = -"WeekNumber";
                """);

            migrationBuilder.DropIndex(name: "IX_CfbSeasonWeekConfigs_Season_EspnWeekNumber", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropIndex(name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_CfbSeasonWeekConfigs_RequiredPicks", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropColumn(name: "EspnWeekNumber", table: "CfbSeasonWeekConfigs");

            // A skipped CFB week has no week number — InScopeIvLeague is the skip flag.
            migrationBuilder.AlterColumn<int>(
                name: "IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
            migrationBuilder.Sql("""UPDATE "CfbSeasonWeekConfigs" SET "IvLeagueWeekNumber" = NULL WHERE NOT "InScopeIvLeague";""");

            migrationBuilder.AddColumn<bool>(
                name: "InScopeIvLeague",
                table: "NflSeasonWeekConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Shared WeekType / ScoringFormat enum names on both tables.
            migrationBuilder.Sql("""
                UPDATE "NflSeasonWeekConfigs" SET
                    "WeekType" = CASE "WeekType"
                        WHEN 'Regular Season' THEN 'RegularSeason'
                        WHEN 'Wild Card' THEN 'Playoff'
                        WHEN 'Divisional' THEN 'Playoff'
                        WHEN 'Conference Championship' THEN 'Playoff'
                        WHEN 'Super Bowl' THEN 'Playoff'
                    END,
                    "ScoringFormat" = CASE "ScoringFormat"
                        WHEN 'Standard' THEN 'Standard'
                        WHEN 'NFL Dvisional' THEN 'Divisional'
                        WHEN 'NFL Conference Championship' THEN 'Conference'
                        WHEN 'NFL Super Bowl' THEN 'Final'
                    END;
                UPDATE "CfbSeasonWeekConfigs" SET
                    "WeekType" = CASE "WeekType"
                        WHEN 'Regular Season' THEN 'RegularSeason'
                        WHEN 'Conference Championships' THEN 'ConferenceChampionships'
                        WHEN 'FBS Playoff' THEN 'Playoff'
                        WHEN 'Dead' THEN 'Dead'
                    END,
                    "ScoringFormat" = CASE "ScoringFormat"
                        WHEN 'Standard' THEN 'Standard'
                        WHEN 'NFLDivisional' THEN 'Divisional'
                        WHEN 'NFLConference' THEN 'Conference'
                        WHEN 'NFLSuperBowl' THEN 'Final'
                        WHEN 'NA' THEN 'None'
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "IvLeagueWeekNumber" },
                unique: true,
                filter: "\"IvLeagueWeekNumber\" IS NOT NULL");

            // A week's postseason flag matches its WeekType (frizat-b4g), on both tables.
            foreach (var table in new[] { "NflSeasonWeekConfigs", "CfbSeasonWeekConfigs" })
            {
                migrationBuilder.AddCheckConstraint(
                    name: $"CK_{table}_IsPostSeason",
                    table: table,
                    sql: "\"IsPostSeason\" = (\"WeekType\" IN ('ConferenceChampionships', 'Playoff'))");
            }
            migrationBuilder.AddCheckConstraint(
                name: "CK_NflSeasonWeekConfigs_ScoringFormat",
                table: "NflSeasonWeekConfigs",
                sql: "(\"ScoringFormat\" = 'Standard' AND \"RequiredPicks\" = 4) OR (\"ScoringFormat\" = 'Divisional' AND \"RequiredPicks\" = 3) OR (\"ScoringFormat\" = 'Conference' AND \"RequiredPicks\" = 2) OR (\"ScoringFormat\" = 'Final' AND \"RequiredPicks\" = 1) OR (\"ScoringFormat\" = 'None' AND \"RequiredPicks\" = 0)");
            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_InScope",
                table: "CfbSeasonWeekConfigs",
                sql: "\"InScopeIvLeague\" = (\"IvLeagueWeekNumber\" IS NOT NULL)");
            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_RequiredPicks",
                table: "CfbSeasonWeekConfigs",
                sql: "(\"InScopeIvLeague\" AND \"RequiredPicks\" BETWEEN 1 AND 4) OR (NOT \"InScopeIvLeague\" AND \"RequiredPicks\" = 0)");
            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_ScoringFormat",
                table: "CfbSeasonWeekConfigs",
                sql: "(\"ScoringFormat\" = 'Standard' AND \"RequiredPicks\" = 4) OR (\"ScoringFormat\" = 'Divisional' AND \"RequiredPicks\" = 3) OR (\"ScoringFormat\" = 'Conference' AND \"RequiredPicks\" = 2) OR (\"ScoringFormat\" = 'Final' AND \"RequiredPicks\" = 1) OR (\"ScoringFormat\" = 'None' AND \"RequiredPicks\" = 0)");

            migrationBuilder.DropTable(name: "CfbSlates");

            migrationBuilder.AddForeignKey(
                name: "FK_CfbPicks_CfbSeasonWeekConfigs_CfbSlateId",
                table: "CfbPicks",
                column: "CfbSlateId",
                principalTable: "CfbSeasonWeekConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_CfbScores_CfbSeasonWeekConfigs_CfbSlateId",
                table: "CfbScores",
                column: "CfbSlateId",
                principalTable: "CfbSeasonWeekConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_CfbSpreads_CfbSeasonWeekConfigs_CfbSlateId",
                table: "CfbSpreads",
                column: "CfbSlateId",
                principalTable: "CfbSeasonWeekConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Best-effort rollback: CfbSlates is rebuilt from the in-scope control-table rows with
        /// Id = the row's Id (so every CfbSlateId stays valid without re-pointing), the old text
        /// values are restored, and ESPN's week number is restored as the slate number (the exact
        /// ESPN numbering for CFP weeks is not recoverable — re-check those rows after a rollback).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_CfbPicks_CfbSeasonWeekConfigs_CfbSlateId", table: "CfbPicks");
            migrationBuilder.DropForeignKey(name: "FK_CfbScores_CfbSeasonWeekConfigs_CfbSlateId", table: "CfbScores");
            migrationBuilder.DropForeignKey(name: "FK_CfbSpreads_CfbSeasonWeekConfigs_CfbSlateId", table: "CfbSpreads");

            migrationBuilder.DropCheckConstraint(name: "CK_NflSeasonWeekConfigs_ScoringFormat", table: "NflSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_NflSeasonWeekConfigs_IsPostSeason", table: "NflSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_CfbSeasonWeekConfigs_IsPostSeason", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_CfbSeasonWeekConfigs_InScope", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_CfbSeasonWeekConfigs_RequiredPicks", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropCheckConstraint(name: "CK_CfbSeasonWeekConfigs_ScoringFormat", table: "CfbSeasonWeekConfigs");
            migrationBuilder.DropIndex(name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber", table: "CfbSeasonWeekConfigs");

            migrationBuilder.Sql("""
                UPDATE "NflSeasonWeekConfigs" SET
                    "WeekType" = CASE WHEN "WeekType" = 'RegularSeason' THEN 'Regular Season'
                        WHEN "WeekId" = 19 THEN 'Wild Card' WHEN "WeekId" = 20 THEN 'Divisional'
                        WHEN "WeekId" = 21 THEN 'Conference Championship' ELSE 'Super Bowl' END,
                    "ScoringFormat" = CASE "ScoringFormat" WHEN 'Standard' THEN 'Standard' WHEN 'Divisional' THEN 'NFL Dvisional'
                        WHEN 'Conference' THEN 'NFL Conference Championship' ELSE 'NFL Super Bowl' END;
                UPDATE "CfbSeasonWeekConfigs" SET
                    "WeekType" = CASE "WeekType" WHEN 'RegularSeason' THEN 'Regular Season'
                        WHEN 'ConferenceChampionships' THEN 'Conference Championships' WHEN 'Playoff' THEN 'FBS Playoff' ELSE 'Dead' END,
                    "ScoringFormat" = CASE "ScoringFormat" WHEN 'Standard' THEN 'Standard' WHEN 'Divisional' THEN 'NFLDivisional'
                        WHEN 'Conference' THEN 'NFLConference' WHEN 'Final' THEN 'NFLSuperBowl' ELSE 'NA' END;
                UPDATE "CfbSeasonWeekConfigs" SET "IvLeagueWeekNumber" = 99 WHERE "IvLeagueWeekNumber" IS NULL;
                """);
            migrationBuilder.DropColumn(name: "InScopeIvLeague", table: "NflSeasonWeekConfigs");
            migrationBuilder.AlterColumn<int>(
                name: "IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EspnWeekNumber",
                table: "CfbSeasonWeekConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Unique per season: in-scope rows take their slate number, skipped rows 100+ (no
            // accurate ESPN number to restore).
            migrationBuilder.Sql("""
                UPDATE "CfbSeasonWeekConfigs" c SET "EspnWeekNumber" = CASE WHEN c."InScopeIvLeague" THEN c."IvLeagueWeekNumber"
                    ELSE 100 + (SELECT COUNT(*) FROM "CfbSeasonWeekConfigs" o WHERE o."Season" = c."Season" AND NOT o."InScopeIvLeague" AND o."Id" < c."Id") END;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_EspnWeekNumber",
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "EspnWeekNumber" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_IvLeagueWeekNumber",
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "IvLeagueWeekNumber" },
                unique: true,
                filter: "\"IvLeagueWeekNumber\" <> 99");
            migrationBuilder.AddCheckConstraint(
                name: "CK_CfbSeasonWeekConfigs_RequiredPicks",
                table: "CfbSeasonWeekConfigs",
                sql: "\"RequiredPicks\" BETWEEN 1 AND 4 OR (\"IvLeagueWeekNumber\" = 99 AND \"RequiredPicks\" = 0)");

            migrationBuilder.RenameColumn(name: "WeekNumber", table: "CfbRankings", newName: "EspnWeekNumber");
            migrationBuilder.RenameIndex(
                name: "IX_CfbRankings_Season_WeekNumber_TeamAbbreviation",
                table: "CfbRankings",
                newName: "IX_CfbRankings_Season_EspnWeekNumber_TeamAbbreviation");

            migrationBuilder.CreateTable(
                name: "CfbSlates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    SlateNumber = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    SlateType = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstGameUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EspnWeekNumber = table.Column<int>(type: "integer", nullable: true),
                    ScoringFormat = table.Column<string>(type: "text", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CfbSlates", x => x.Id);
                });
            migrationBuilder.Sql("""
                INSERT INTO "CfbSlates" ("Id", "Season", "SlateNumber", "Label", "SlateType", "StartDate", "EndDate", "EspnWeekNumber", "ScoringFormat")
                OVERRIDING SYSTEM VALUE
                SELECT c."Id", c."Season", c."IvLeagueWeekNumber", c."WeekLabel", '', c."WeekStartDate", c."WeekEndDate", c."EspnWeekNumber", c."ScoringFormat"
                FROM "CfbSeasonWeekConfigs" c WHERE c."InScopeIvLeague";
                SELECT setval(pg_get_serial_sequence('"CfbSlates"', 'Id'), GREATEST((SELECT MAX("Id") FROM "CfbSlates"), 1));
                """);
            migrationBuilder.CreateIndex(
                name: "IX_CfbSlates_Season_SlateNumber",
                table: "CfbSlates",
                columns: new[] { "Season", "SlateNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CfbPicks_CfbSlates_CfbSlateId",
                table: "CfbPicks",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_CfbScores_CfbSlates_CfbSlateId",
                table: "CfbScores",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_CfbSpreads_CfbSlates_CfbSlateId",
                table: "CfbSpreads",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
