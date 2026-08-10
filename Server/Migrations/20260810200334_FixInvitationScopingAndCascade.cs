using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class FixInvitationScopingAndCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_AspNetUsers_RegisteredUserId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Email",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                table: "Invitations",
                column: "Email",
                unique: true,
                filter: "\"LeagueId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email_LeagueId",
                table: "Invitations",
                columns: new[] { "Email", "LeagueId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_AspNetUsers_RegisteredUserId",
                table: "Invitations",
                column: "RegisteredUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations",
                column: "LeagueId",
                principalTable: "LeagueInfo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_AspNetUsers_RegisteredUserId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Email",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Email_LeagueId",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                table: "Invitations",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_AspNetUsers_RegisteredUserId",
                table: "Invitations",
                column: "RegisteredUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_LeagueInfo_LeagueId",
                table: "Invitations",
                column: "LeagueId",
                principalTable: "LeagueInfo",
                principalColumn: "Id");
        }
    }
}
