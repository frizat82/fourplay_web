using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class CfbSeasonWeekConfigTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EspnWeekNumber",
                table: "CfbSlates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringFormat",
                table: "CfbSlates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CfbSeasonWeekConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    EspnWeekNumber = table.Column<int>(type: "integer", nullable: false),
                    IvLeagueWeekNumber = table.Column<int>(type: "integer", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekType = table.Column<string>(type: "text", nullable: false),
                    ScoringFormat = table.Column<string>(type: "text", nullable: false),
                    InScopeIvLeague = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CfbSeasonWeekConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CfbSeasonWeekConfigs_Season_EspnWeekNumber",
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "EspnWeekNumber" },
                unique: true);

            // Seed 2026-27 season control table from Google Sheet
            // IV_LEAGUE_WEEK_NUMBER=99 = excluded; InScopeIvLeague=false = skip
            migrationBuilder.InsertData(
                table: "CfbSeasonWeekConfigs",
                columns: new[] { "Season", "EspnWeekNumber", "IvLeagueWeekNumber", "WeekStartDate", "WeekEndDate", "WeekType", "ScoringFormat", "InScopeIvLeague", "Notes" },
                values: new object[,]
                {
                    { 2026,  0, 99, new DateOnly(2026,  8, 25), new DateOnly(2026,  8, 31), "Regular Season",        "NA",            false, "Thu 8/27 openers + Sat 8/29 slate (NC vs TCU in Dublin, etc.)" },
                    { 2026,  1,  1, new DateOnly(2026,  9,  1), new DateOnly(2026,  9,  7), "Regular Season",        "Standard",      true,  "Begins Thu 9/3; bulk Sat 9/5 (Labor Day weekend)" },
                    { 2026,  2,  2, new DateOnly(2026,  9,  8), new DateOnly(2026,  9, 14), "Regular Season",        "Standard",      true,  "Ohio State at Texas (Sat 9/12) + Oklahoma at Michigan" },
                    { 2026,  3,  3, new DateOnly(2026,  9, 15), new DateOnly(2026,  9, 21), "Regular Season",        "Standard",      true,  "Big 12 Union Jack Classic at Wembley (Sat 9/19)" },
                    { 2026,  4,  4, new DateOnly(2026,  9, 22), new DateOnly(2026,  9, 28), "Regular Season",        "Standard",      true,  "Loaded SEC slate (Sat 9/26): Oklahoma at Georgia, Texas A&M at LSU, Texas at Tennessee" },
                    { 2026,  5,  5, new DateOnly(2026,  9, 29), new DateOnly(2026, 10,  5), "Regular Season",        "Standard",      true,  "Washington at USC (Sat 10/3)" },
                    { 2026,  6,  6, new DateOnly(2026, 10,  6), new DateOnly(2026, 10, 12), "Regular Season",        "Standard",      true,  "Red River Showdown: Texas vs Oklahoma at the Cotton Bowl (Sat 10/10)" },
                    { 2026,  7,  7, new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 19), "Regular Season",        "Standard",      true,  "Third Saturday in October: Alabama at Tennessee + Auburn at Georgia (Sat 10/17)" },
                    { 2026,  8,  8, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 26), "Regular Season",        "Standard",      true,  "Boise State at Washington State (Sat 10/24)" },
                    { 2026,  9,  9, new DateOnly(2026, 10, 27), new DateOnly(2026, 11,  2), "Regular Season",        "Standard",      true,  "Ohio State at USC (Sat 10/31)" },
                    { 2026, 10, 10, new DateOnly(2026, 11,  3), new DateOnly(2026, 11,  9), "Regular Season",        "Standard",      true,  "Oregon at Ohio State (Sat 11/7)" },
                    { 2026, 11, 11, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 16), "Regular Season",        "Standard",      true,  "Ole Miss vs Georgia in Oxford (Sat 11/14)" },
                    { 2026, 12, 12, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 23), "Regular Season",        "Standard",      true,  "LSU at Tennessee (Sat 11/21)" },
                    { 2026, 13, 13, new DateOnly(2026, 11, 24), new DateOnly(2026, 11, 30), "Regular Season",        "Standard",      true,  "Thanksgiving (Thu 11/26) + rivalry Saturday (11/28)" },
                    { 2026, 14, 14, new DateOnly(2026, 12,  1), new DateOnly(2026, 12,  7), "Conference Championships", "Standard",  true,  "Conference championship weekend (Fri 12/4 - Sat 12/5)" },
                    { 2026, 15, 99, new DateOnly(2026, 12,  8), new DateOnly(2026, 12, 14), "Regular Season",        "NA",            false, "Army-Navy Game (Sat 12/12)" },
                    { 2026, 16, 15, new DateOnly(2026, 12, 15), new DateOnly(2026, 12, 21), "FBS Playoff",           "NFLDivisional", true,  "CFP First Round: Fri 12/18 (1 game) + Sat 12/19 (3 games, campus sites)" },
                    { 2026, 17, 99, new DateOnly(2026, 12, 22), new DateOnly(2026, 12, 28), "Dead",                  "NA",            false, "No CFP games - gap week between First Round and Quarterfinals" },
                    { 2026, 18, 16, new DateOnly(2026, 12, 29), new DateOnly(2027,  1,  4), "FBS Playoff",           "NFLDivisional", true,  "CFP Quarterfinals: Fiesta Bowl Wed 12/30; Cotton, Peach & Rose Bowls Fri 1/1" },
                    { 2026, 19, 99, new DateOnly(2027,  1,  5), new DateOnly(2027,  1, 11), "Dead",                  "NA",            false, "No CFP games - gap week before Semifinals" },
                    { 2026, 20, 17, new DateOnly(2027,  1, 12), new DateOnly(2027,  1, 18), "FBS Playoff",           "NFLConference", true,  "CFP Semifinals: Orange Bowl Thu 1/14; Sugar Bowl Fri 1/15" },
                    { 2026, 21, 18, new DateOnly(2027,  1, 19), new DateOnly(2027,  1, 25), "FBS Playoff",           "NFLSuperBowl",  true,  "CFP National Championship: Mon 1/25 at Allegiant Stadium, Las Vegas" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CfbSeasonWeekConfigs");

            migrationBuilder.DropColumn(
                name: "EspnWeekNumber",
                table: "CfbSlates");

            migrationBuilder.DropColumn(
                name: "ScoringFormat",
                table: "CfbSlates");
        }
    }
}
