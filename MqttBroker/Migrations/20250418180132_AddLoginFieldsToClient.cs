using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MqttBroker.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginFieldsToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Clients",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Clients");
        }
    }
}
