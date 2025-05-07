using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MqttBroker.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicNameToNamedSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TopicName",
                table: "NamedSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TopicName",
                table: "NamedSubscriptions");
        }
    }
}
