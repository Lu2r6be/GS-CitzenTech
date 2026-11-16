using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Termometro.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Achados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Exposicao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CategoriaPii = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClimasExternos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Estacao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ObservadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TempMaxC = table.Column<double>(type: "double precision", nullable: false),
                    TempMinC = table.Column<double>(type: "double precision", nullable: false),
                    Umidade = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClimasExternos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiasRisco",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Estacao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RrPrevisto = table.Column<double>(type: "double precision", nullable: false),
                    Corte = table.Column<double>(type: "double precision", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ModeloVersao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiasRisco", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Limiares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AzulAbaixoDe = table.Column<double>(type: "double precision", nullable: false),
                    VerdeAte = table.Column<double>(type: "double precision", nullable: false),
                    AmareloAte = table.Column<double>(type: "double precision", nullable: false),
                    Versao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Origem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Limiares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Microareas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Microareas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Residentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    DataNascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    CondicoesSaude = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TelefoneContato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MoraSozinho = table.Column<bool>(type: "boolean", nullable: false),
                    TemDoencaCronica = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Residentes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Consentimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ConcedidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevogadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consentimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consentimentos_Residentes_ResidenteId",
                        column: x => x.ResidenteId,
                        principalTable: "Residentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Domicilios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    MicroareaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    TipoCobertura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Andar = table.Column<int>(type: "integer", nullable: true),
                    NivelConfirmado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NivelCandidato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LeiturasDoCandidato = table.Column<int>(type: "integer", nullable: false),
                    UltimaLeituraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domicilios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Domicilios_Microareas_MicroareaId",
                        column: x => x.MicroareaId,
                        principalTable: "Microareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Domicilios_Residentes_ResidenteId",
                        column: x => x.ResidenteId,
                        principalTable: "Residentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Papel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MicroareaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResidenteId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Microareas_MicroareaId",
                        column: x => x.MicroareaId,
                        principalTable: "Microareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Usuarios_Residentes_ResidenteId",
                        column: x => x.ResidenteId,
                        principalTable: "Residentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dispositivos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DomicilioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceKeyHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispositivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dispositivos_Domicilios_DomicilioId",
                        column: x => x.DomicilioId,
                        principalTable: "Domicilios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Visitas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DomicilioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcsId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanejadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RealizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Origem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SincronizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visitas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visitas_Domicilios_DomicilioId",
                        column: x => x.DomicilioId,
                        principalTable: "Domicilios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visitas_Usuarios_AcsId",
                        column: x => x.AcsId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leituras",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DispositivoId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TemperaturaC = table.Column<double>(type: "double precision", nullable: false),
                    Umidade = table.Column<double>(type: "double precision", nullable: false),
                    IndiceCalorC = table.Column<double>(type: "double precision", nullable: false),
                    Nivel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leituras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leituras_Dispositivos_DispositivoId",
                        column: x => x.DispositivoId,
                        principalTable: "Dispositivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achados_CriadoEm",
                table: "Achados",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_ClimasExternos_Estacao_ObservadoEm",
                table: "ClimasExternos",
                columns: new[] { "Estacao", "ObservadoEm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consentimentos_ResidenteId",
                table: "Consentimentos",
                column: "ResidenteId");

            migrationBuilder.CreateIndex(
                name: "IX_DiasRisco_Data_Estacao",
                table: "DiasRisco",
                columns: new[] { "Data", "Estacao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_DomicilioId",
                table: "Dispositivos",
                column: "DomicilioId");

            migrationBuilder.CreateIndex(
                name: "IX_Domicilios_MicroareaId",
                table: "Domicilios",
                column: "MicroareaId");

            migrationBuilder.CreateIndex(
                name: "IX_Domicilios_ResidenteId",
                table: "Domicilios",
                column: "ResidenteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leituras_DispositivoId_RecebidaEm",
                table: "Leituras",
                columns: new[] { "DispositivoId", "RecebidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Limiares_Versao",
                table: "Limiares",
                column: "Versao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Microareas_Nome",
                table: "Microareas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Residentes_Cpf",
                table: "Residentes",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_MicroareaId",
                table: "Usuarios",
                column: "MicroareaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ResidenteId",
                table: "Usuarios",
                column: "ResidenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Visitas_AcsId",
                table: "Visitas",
                column: "AcsId");

            migrationBuilder.CreateIndex(
                name: "IX_Visitas_DomicilioId_RealizadaEm",
                table: "Visitas",
                columns: new[] { "DomicilioId", "RealizadaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achados");

            migrationBuilder.DropTable(
                name: "ClimasExternos");

            migrationBuilder.DropTable(
                name: "Consentimentos");

            migrationBuilder.DropTable(
                name: "DiasRisco");

            migrationBuilder.DropTable(
                name: "Leituras");

            migrationBuilder.DropTable(
                name: "Limiares");

            migrationBuilder.DropTable(
                name: "Visitas");

            migrationBuilder.DropTable(
                name: "Dispositivos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Domicilios");

            migrationBuilder.DropTable(
                name: "Microareas");

            migrationBuilder.DropTable(
                name: "Residentes");
        }
    }
}
