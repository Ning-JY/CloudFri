using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFri.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractId",
                table: "Receipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Members",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "Members",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ItemsJson",
                table: "GcfxParses",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsFinal",
                table: "DeliverableSets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // 存量成员默认归入工程咨询事业部
            migrationBuilder.Sql("UPDATE `Members` SET `Department` = '工程咨询事业部' WHERE `Department` IS NULL OR `Department` = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ItemsJson",
                table: "GcfxParses");

            migrationBuilder.DropColumn(
                name: "IsFinal",
                table: "DeliverableSets");
        }
    }
}
