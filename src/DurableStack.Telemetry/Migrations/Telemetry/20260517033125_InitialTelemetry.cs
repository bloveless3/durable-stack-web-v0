using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DurableStack.Telemetry.Migrations.Telemetry
{
    /// <inheritdoc />
    public partial class InitialTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantPublicId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EnvironmentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: true),
                    WorkerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DurationMs = table.Column<double>(type: "double precision", nullable: true),
                    ErrorType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_events_telemetry_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "telemetry_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_batches_TenantPublicId_IdempotencyKey",
                table: "telemetry_batches",
                columns: new[] { "TenantPublicId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_batches_TenantPublicId_ReceivedAtUtc",
                table: "telemetry_batches",
                columns: new[] { "TenantPublicId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_BatchId",
                table: "telemetry_events",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_EventType_OccurredAtUtc",
                table: "telemetry_events",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_RunId",
                table: "telemetry_events",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_events");

            migrationBuilder.DropTable(
                name: "telemetry_batches");
        }
    }
}
