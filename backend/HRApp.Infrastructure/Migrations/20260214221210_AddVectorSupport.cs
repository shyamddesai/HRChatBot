using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace HRApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
            
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "Documents",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(float[]),
                oldType: "real[]",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradeNumber",
                table: "Employees",
                type: "integer",
                nullable: true,
                computedColumnSql: "CAST(SUBSTRING(\"Grade\" FROM '\\d+') AS INTEGER)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_GradeNumber",
                table: "Employees",
                column: "GradeNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_GradeNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GradeNumber",
                table: "Employees");

            migrationBuilder.AlterColumn<float[]>(
                name: "Embedding",
                table: "Documents",
                type: "real[]",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);
        }
    }
}
