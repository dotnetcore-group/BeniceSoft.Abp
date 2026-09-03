using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "am_roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false, comment: "扩展属性"),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    CreatorId = table.Column<long>(type: "bigint", nullable: false, comment: "创建人Id"),
                    CreatorName = table.Column<string>(type: "text", nullable: false, comment: "创建人姓名"),
                    LastModificationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "最新修改时间"),
                    LastModifierId = table.Column<long>(type: "bigint", nullable: true, comment: "最新修改人Id"),
                    LastModifierName = table.Column<string>(type: "text", nullable: true, comment: "最新修改人姓名"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否已删除"),
                    DeletionTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "删除时间"),
                    DeleterId = table.Column<long>(type: "bigint", nullable: true, comment: "删除人Id"),
                    DeleterName = table.Column<string>(type: "text", nullable: true, comment: "删除人姓名")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_am_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "am_users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false, comment: "扩展属性"),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    CreatorId = table.Column<long>(type: "bigint", nullable: false, comment: "创建人Id"),
                    CreatorName = table.Column<string>(type: "text", nullable: false, comment: "创建人姓名"),
                    LastModificationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "最新修改时间"),
                    LastModifierId = table.Column<long>(type: "bigint", nullable: true, comment: "最新修改人Id"),
                    LastModifierName = table.Column<string>(type: "text", nullable: true, comment: "最新修改人姓名"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否已删除"),
                    DeletionTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "删除时间"),
                    DeleterId = table.Column<long>(type: "bigint", nullable: true, comment: "删除人Id"),
                    DeleterName = table.Column<string>(type: "text", nullable: true, comment: "删除人姓名")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_am_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bulk_demo_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    BatchTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_demo_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    OrderTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatchTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "am_userroles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false, comment: "用户id"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false, comment: "角色id")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_am_userroles", x => new { x.UserId, x.RoleId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bulk_demo_items_BatchTag",
                table: "bulk_demo_items",
                column: "BatchTag");

            migrationBuilder.CreateIndex(
                name: "IX_bulk_demo_items_Code",
                table: "bulk_demo_items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_Code",
                table: "products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_BatchTag",
                table: "sales_orders",
                column: "BatchTag");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_OrderNo",
                table: "sales_orders",
                column: "OrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_OrderTime",
                table: "sales_orders",
                column: "OrderTime");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_ProductCode",
                table: "sales_orders",
                column: "ProductCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "am_roles");

            migrationBuilder.DropTable(
                name: "am_userroles");

            migrationBuilder.DropTable(
                name: "bulk_demo_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "sales_orders");

            migrationBuilder.DropTable(
                name: "am_users");
        }
    }
}
