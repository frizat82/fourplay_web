using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueIdToInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Invitations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_LeagueId",
                table: "Invitations",
                column: "LeagueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations",
                column: "LeagueId",
                principalTable: "LeagueInfo",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_LeagueId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Invitations");
        }
    }
}
