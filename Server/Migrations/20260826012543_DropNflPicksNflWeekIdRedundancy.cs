using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropNflPicksNflWeekIdRedundancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NflPicks_NflWeeks_NflWeekId",
                table: "NflPicks");

            migrationBuilder.DropIndex(
                name: "IX_NflPicks_NflWeekId",
                table: "NflPicks");

            migrationBuilder.DropColumn(
                name: "NflWeekId",
                table: "NflPicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NflWeekId",
                table: "NflPicks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_NflWeekId",
                table: "NflPicks",
                column: "NflWeekId");

            migrationBuilder.AddForeignKey(
                name: "FK_NflPicks_NflWeeks_NflWeekId",
                table: "NflPicks",
                column: "NflWeekId",
                principalTable: "NflWeeks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
