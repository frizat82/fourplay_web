using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class PerSportNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences");

            migrationBuilder.AddColumn<int>(
                name: "Sport",
                table: "NotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_Sport",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "Sport" },
                unique: true);

            // Existing rows become NFL (default 0); copy each as a CFB (1) row so nobody's alerts
            // change until they edit one sport's settings.
            migrationBuilder.Sql(@"INSERT INTO ""NotificationPreferences"" (""Sport"", ""UserId"", ""NotifyMineBloodyDuringGame"", ""NotifyMineBloodyAtFinal"", ""NotifyMineCoveringDuringGame"", ""NotifyMineCoveringAtFinal"", ""NotifyOthersBloodyDuringGame"", ""NotifyOthersBloodyAtFinal"", ""NotifyOthersCoveringDuringGame"", ""NotifyOthersCoveringAtFinal"", ""NotifyWeekResult"", ""UpdatedAt"")
                SELECT 1, ""UserId"", ""NotifyMineBloodyDuringGame"", ""NotifyMineBloodyAtFinal"", ""NotifyMineCoveringDuringGame"", ""NotifyMineCoveringAtFinal"", ""NotifyOthersBloodyDuringGame"", ""NotifyOthersBloodyAtFinal"", ""NotifyOthersCoveringDuringGame"", ""NotifyOthersCoveringAtFinal"", ""NotifyWeekResult"", ""UpdatedAt"" FROM ""NotificationPreferences"" WHERE ""Sport"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""NotificationPreferences"" WHERE ""Sport"" <> 0;");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_UserId_Sport",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "NotificationPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);
        }
    }
}
