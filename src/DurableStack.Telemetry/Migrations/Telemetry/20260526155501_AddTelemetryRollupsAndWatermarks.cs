using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DurableStack.Telemetry.Migrations.Telemetry
{
    /// <inheritdoc />
    public partial class AddTelemetryRollupsAndWatermarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_bucket_rollups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantPublicId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BucketSize = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BucketStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RunStarted = table.Column<int>(type: "integer", nullable: false),
                    RunSucceeded = table.Column<int>(type: "integer", nullable: false),
                    RunFailed = table.Column<int>(type: "integer", nullable: false),
                    RunRetried = table.Column<int>(type: "integer", nullable: false),
                    HeartbeatCount = table.Column<int>(type: "integer", nullable: false),
                    LastEventAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ComputedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_bucket_rollups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_failure_group_rollups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantPublicId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BucketSize = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BucketStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    FirstOccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastOccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ComputedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_failure_group_rollups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_rollup_watermarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantPublicId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BucketSize = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastRolledUpBucketStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_rollup_watermarks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_bucket_rollups_BucketSize_BucketStartUtc",
                table: "telemetry_bucket_rollups",
                columns: new[] { "BucketSize", "BucketStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_bucket_rollups_TenantPublicId_BucketSize_BucketSt~",
                table: "telemetry_bucket_rollups",
                columns: new[] { "TenantPublicId", "BucketSize", "BucketStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_failure_group_rollups_BucketSize_BucketStartUtc",
                table: "telemetry_failure_group_rollups",
                columns: new[] { "BucketSize", "BucketStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_failure_group_rollups_TenantPublicId_BucketSize_B~",
                table: "telemetry_failure_group_rollups",
                columns: new[] { "TenantPublicId", "BucketSize", "BucketStartUtc", "JobName", "ErrorType", "ErrorMessage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_rollup_watermarks_TenantPublicId_BucketSize",
                table: "telemetry_rollup_watermarks",
                columns: new[] { "TenantPublicId", "BucketSize" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_bucket_rollups");

            migrationBuilder.DropTable(
                name: "telemetry_failure_group_rollups");

            migrationBuilder.DropTable(
                name: "telemetry_rollup_watermarks");
        }
    }
}
