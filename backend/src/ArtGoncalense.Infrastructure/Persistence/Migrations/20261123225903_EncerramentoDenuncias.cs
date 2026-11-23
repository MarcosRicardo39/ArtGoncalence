using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGoncalense.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncerramentoDenuncias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EncerradaEm",
                table: "Denuncias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EncerradaPorId",
                table: "Denuncias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaEncerramento",
                table: "Denuncias",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_EncerradaPorId",
                table: "Denuncias",
                column: "EncerradaPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Denuncias_Usuarios_EncerradaPorId",
                table: "Denuncias",
                column: "EncerradaPorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Denuncias_Usuarios_EncerradaPorId",
                table: "Denuncias");

            migrationBuilder.DropIndex(
                name: "IX_Denuncias_EncerradaPorId",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "EncerradaEm",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "EncerradaPorId",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "JustificativaEncerramento",
                table: "Denuncias");
        }
    }
}
