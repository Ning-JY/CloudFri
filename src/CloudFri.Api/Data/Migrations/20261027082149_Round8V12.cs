using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFri.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round8V12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProjectFileId",
                table: "GcfxParses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ProjectDocId",
                table: "GcfxParses",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectDocId",
                table: "GcfxParses");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectFileId",
                table: "GcfxParses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
