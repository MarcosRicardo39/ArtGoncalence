using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGoncalense.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedidosDePrivacidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoConservacaoContato",
                table: "Denuncias",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PedidosExclusao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TitularId = table.Column<Guid>(type: "uuid", nullable: false),
                    IlustracaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    SolicitadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcluidoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosExclusao", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosExclusao_TitularId_ConcluidoEm",
                table: "PedidosExclusao",
                columns: new[] { "TitularId", "ConcluidoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidosExclusao");

            migrationBuilder.DropColumn(
                name: "MotivoConservacaoContato",
                table: "Denuncias");
        }
    }
}
