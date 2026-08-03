using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInvitationIsLeagueOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLeagueOwner",
                table: "Invitations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLeagueOwner",
                table: "Invitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
