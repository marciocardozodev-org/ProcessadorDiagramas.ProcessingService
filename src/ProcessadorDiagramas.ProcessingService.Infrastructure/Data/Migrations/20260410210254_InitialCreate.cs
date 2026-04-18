using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiagramProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagramAnalysisProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputStorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PreprocessedContent = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagramProcessingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagramProcessingAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagramProcessingJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagramProcessingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagramProcessingAttempts_DiagramProcessingJobs_DiagramProc~",
                        column: x => x.DiagramProcessingJobId,
                        principalTable: "DiagramProcessingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagramProcessingResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagramProcessingJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawAiOutput = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagramProcessingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagramProcessingResults_DiagramProcessingJobs_DiagramProce~",
                        column: x => x.DiagramProcessingJobId,
                        principalTable: "DiagramProcessingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagramProcessingAttempts_DiagramProcessingJobId_AttemptNum~",
                table: "DiagramProcessingAttempts",
                columns: new[] { "DiagramProcessingJobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagramProcessingJobs_CorrelationId",
                table: "DiagramProcessingJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagramProcessingJobs_DiagramAnalysisProcessId",
                table: "DiagramProcessingJobs",
                column: "DiagramAnalysisProcessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagramProcessingResults_DiagramProcessingJobId",
                table: "DiagramProcessingResults",
                column: "DiagramProcessingJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagramProcessingAttempts");

            migrationBuilder.DropTable(
                name: "DiagramProcessingResults");

            migrationBuilder.DropTable(
                name: "DiagramProcessingJobs");
        }
    }
}
