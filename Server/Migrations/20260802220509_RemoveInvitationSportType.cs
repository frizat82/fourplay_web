using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInvitationSportType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SportType",
                table: "Invitations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SportType",
                table: "Invitations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
