using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPickLiveNotificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickLiveNotificationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    PickId = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    LastNotifiedCovering = table.Column<bool>(type: "boolean", nullable: true),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    FinalNotifiedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickLiveNotificationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickLiveNotificationStates_LeagueId",
                table: "PickLiveNotificationStates",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_PickLiveNotificationStates_Sport_PickId",
                table: "PickLiveNotificationStates",
                columns: new[] { "Sport", "PickId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickLiveNotificationStates");
        }
    }
}
