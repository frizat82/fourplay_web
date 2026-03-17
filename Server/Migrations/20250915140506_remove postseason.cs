using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class removepostseason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NflPostSeasonPicks");

            migrationBuilder.AddColumn<int>(
                name: "Pick",
                table: "NflPicks",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.DropColumn(name: "LeagueInfoId", table: "NflPicks");

            migrationBuilder.AlterColumn<int>(
                name: "LeagueType",
                table: "LeagueInfo",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pick",
                table: "NflPicks");

            migrationBuilder.AlterColumn<int>(
                name: "LeagueType",
                table: "LeagueInfo",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NflPostSeasonPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeagueInfoId = table.Column<int>(type: "integer", nullable: true),
                    NflWeek = table.Column<int>(type: "integer", nullable: false),
                    Pick = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Team = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NflPostSeasonPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_LeagueInfo_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_LeagueInfo_LeagueInfoId",
                        column: x => x.LeagueInfoId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_LeagueId",
                table: "NflPostSeasonPicks",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_LeagueInfoId",
                table: "NflPostSeasonPicks",
                column: "LeagueInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_UserId_LeagueId_NflWeek_Season_Team_Pick",
                table: "NflPostSeasonPicks",
                columns: new[] { "UserId", "LeagueId", "NflWeek", "Season", "Team", "Pick" },
                unique: true);
        }
    }
}
