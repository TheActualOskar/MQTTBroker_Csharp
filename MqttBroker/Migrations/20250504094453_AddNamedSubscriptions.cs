using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MqttBroker.Migrations
{
    /// <inheritdoc />
    public partial class AddNamedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NamedSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CypherQuery = table.Column<string>(type: "text", nullable: false),
                    ReturnFields = table.Column<string>(type: "text", nullable: false),
                    CreatedByClientId = table.Column<int>(type: "integer", nullable: false),
                    CurrentMatchCount = table.Column<int>(type: "integer", nullable: false),
                    LastResultHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamedSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamedSubscriptions_Clients_CreatedByClientId",
                        column: x => x.CreatedByClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientNamedSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    NamedSubscriptionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientNamedSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientNamedSubscriptions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientNamedSubscriptions_NamedSubscriptions_NamedSubscripti~",
                        column: x => x.NamedSubscriptionId,
                        principalTable: "NamedSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientNamedSubscriptions_ClientId",
                table: "ClientNamedSubscriptions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNamedSubscriptions_NamedSubscriptionId",
                table: "ClientNamedSubscriptions",
                column: "NamedSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_NamedSubscriptions_CreatedByClientId",
                table: "NamedSubscriptions",
                column: "CreatedByClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientNamedSubscriptions");

            migrationBuilder.DropTable(
                name: "NamedSubscriptions");
        }
    }
}
