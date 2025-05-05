using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MqttBroker.Migrations
{
    /// <inheritdoc />
    public partial class NamedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnFields",
                table: "NamedSubscriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnFields",
                table: "NamedSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
