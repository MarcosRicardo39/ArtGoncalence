using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGoncalense.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModeloInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    NomePublico = table.Column<string>(type: "text", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    AvatarPath = table.Column<string>(type: "text", nullable: true),
                    Papel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ilustracoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "text", nullable: false),
                    CaminhoStorage = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ilustracoes", x => x.Id);
                    table.CheckConstraint("CK_Ilustracoes_TamanhoBytes_Positivo", "\"TamanhoBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_Ilustracoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LinksApoio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinksApoio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinksApoio_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcoesModeracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IlustracaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeradorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<int>(type: "integer", nullable: false),
                    Justificativa = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcoesModeracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoesModeracao_Ilustracoes_IlustracaoId",
                        column: x => x.IlustracaoId,
                        principalTable: "Ilustracoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcoesModeracao_Usuarios_ModeradorId",
                        column: x => x.ModeradorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Denuncias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IlustracaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contato = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Denuncias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Denuncias_Ilustracoes_IlustracaoId",
                        column: x => x.IlustracaoId,
                        principalTable: "Ilustracoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesModeracao_IlustracaoId",
                table: "AcoesModeracao",
                column: "IlustracaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcoesModeracao_ModeradorId",
                table: "AcoesModeracao",
                column: "ModeradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_IlustracaoId",
                table: "Denuncias",
                column: "IlustracaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ilustracoes_UsuarioId",
                table: "Ilustracoes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LinksApoio_UsuarioId",
                table: "LinksApoio",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoesModeracao");

            migrationBuilder.DropTable(
                name: "Denuncias");

            migrationBuilder.DropTable(
                name: "LinksApoio");

            migrationBuilder.DropTable(
                name: "Ilustracoes");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
