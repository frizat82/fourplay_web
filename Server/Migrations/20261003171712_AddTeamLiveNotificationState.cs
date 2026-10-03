using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLiveNotificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamLiveNotificationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    Team = table.Column<string>(type: "text", nullable: false),
                    PickType = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    LastNotifiedCovering = table.Column<bool>(type: "boolean", nullable: true),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    FinalNotifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLiveNotificationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLiveNotificationStates_Sport_LeagueId_Team_PickType_Per~",
                table: "TeamLiveNotificationStates",
                columns: new[] { "Sport", "LeagueId", "Team", "PickType", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamLiveNotificationStates");
        }
    }
}
