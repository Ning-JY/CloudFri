using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFri.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round9BudgetReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReviewSubmitAmount",
                table: "Projects",
                type: "decimal(65,30)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewSubmitAmount",
                table: "Projects");
        }
    }
}
