using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFri.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round7V11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNo",
                table: "Receipts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsReceived",
                table: "Receipts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceivedDate",
                table: "Receipts",
                type: "date",
                nullable: true);

            // 存量收款是旧口径下「录入即到账」的记录，全部回填为已收款，到账日期=原记录日期，避免切两段式后已收金额清零
            migrationBuilder.Sql("UPDATE Receipts SET IsReceived = 1, ReceivedDate = Date;");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Projects",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "LibraryDocs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "LibraryDocs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Contracts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvoiceNo",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "IsReceived",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "LibraryDocs");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "LibraryDocs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Contracts");
        }
    }
}
