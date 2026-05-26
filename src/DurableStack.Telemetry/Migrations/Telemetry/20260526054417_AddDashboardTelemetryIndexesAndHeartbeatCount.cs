using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DurableStack.Telemetry.Migrations.Telemetry
{
    /// <inheritdoc />
    public partial class AddDashboardTelemetryIndexesAndHeartbeatCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeartbeatCount",
                table: "telemetry_events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_OccurredAtUtc_BatchId",
                table: "telemetry_events",
                columns: new[] { "OccurredAtUtc", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_WorkerName_OccurredAtUtc",
                table: "telemetry_events",
                columns: new[] { "WorkerName", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telemetry_events_OccurredAtUtc_BatchId",
                table: "telemetry_events");

            migrationBuilder.DropIndex(
                name: "IX_telemetry_events_WorkerName_OccurredAtUtc",
                table: "telemetry_events");

            migrationBuilder.DropColumn(
                name: "HeartbeatCount",
                table: "telemetry_events");
        }
    }
}
