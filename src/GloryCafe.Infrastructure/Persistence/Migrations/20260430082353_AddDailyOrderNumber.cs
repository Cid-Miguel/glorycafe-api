using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GloryCafe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add columns as nullable so existing rows survive insertion.
            migrationBuilder.Sql(
                "ALTER TABLE orders ADD COLUMN \"OrderDate\" date NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE orders ADD COLUMN \"DailyOrderNumber\" integer NULL;");

            // 2. Backfill OrderDate from CreatedAt converted to the business
            //    timezone (Brisbane is UTC+10 year-round, no DST), then number
            //    rows sequentially within each day ordered by Id.
            migrationBuilder.Sql(@"
                UPDATE orders
                SET ""OrderDate"" = (""CreatedAt"" AT TIME ZONE 'Australia/Brisbane')::date
                WHERE ""OrderDate"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""OrderDate"" ORDER BY ""Id"") AS rn
                    FROM orders
                )
                UPDATE orders o
                SET ""DailyOrderNumber"" = numbered.rn
                FROM numbered
                WHERE o.""Id"" = numbered.""Id"";
            ");

            // 3. Lock the columns down now that every row has a real value.
            migrationBuilder.Sql(
                "ALTER TABLE orders ALTER COLUMN \"OrderDate\" SET NOT NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE orders ALTER COLUMN \"DailyOrderNumber\" SET NOT NULL;");

            // 4. Unique index guarantees no duplicate (date, number) pairs and
            //    also speeds up the MAX(DailyOrderNumber) lookup in the allocator.
            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderDate_DailyOrderNumber",
                table: "orders",
                columns: new[] { "OrderDate", "DailyOrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_OrderDate_DailyOrderNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DailyOrderNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "OrderDate",
                table: "orders");
        }
    }
}
