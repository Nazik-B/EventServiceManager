using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column that already exists on every table.
            // The migration only updates the model snapshot so EF treats xmin as a concurrency token.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
