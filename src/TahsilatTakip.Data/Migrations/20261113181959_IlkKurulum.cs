using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TahsilatTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class IlkKurulum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ayarlar",
                columns: table => new
                {
                    Anahtar = table.Column<string>(type: "TEXT", nullable: false),
                    Deger = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ayarlar", x => x.Anahtar);
                });

            migrationBuilder.CreateTable(
                name: "HizmetTurleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Sistem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HizmetTurleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IslemGunlugu",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: true),
                    Tablo = table.Column<string>(type: "TEXT", nullable: false),
                    KayitId = table.Column<string>(type: "TEXT", nullable: false),
                    Islem = table.Column<string>(type: "TEXT", nullable: false),
                    Detay = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IslemGunlugu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciAdi = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    AdSoyad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SifreHash = table.Column<string>(type: "TEXT", nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SonGirisTarihi = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdemeGruplari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ad = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdemeGruplari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mukellefler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Unvan = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    VknTckn = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", nullable: true),
                    Eposta = table.Column<string>(type: "TEXT", nullable: true),
                    Adres = table.Column<string>(type: "TEXT", nullable: true),
                    OdemeSorumlusuAd = table.Column<string>(type: "TEXT", nullable: true),
                    OdemeSorumlusuTelefon = table.Column<string>(type: "TEXT", nullable: true),
                    OdemeGunu = table.Column<int>(type: "INTEGER", nullable: true),
                    OdemeTercihi = table.Column<int>(type: "INTEGER", nullable: false),
                    Durum = table.Column<int>(type: "INTEGER", nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AyrilisTarihi = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notlar = table.Column<string>(type: "TEXT", nullable: true),
                    OdemeGrubuId = table.Column<int>(type: "INTEGER", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mukellefler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mukellefler_OdemeGruplari_OdemeGrubuId",
                        column: x => x.OdemeGrubuId,
                        principalTable: "OdemeGruplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GorusmeNotlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MukellefId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kanal = table.Column<int>(type: "INTEGER", nullable: false),
                    Not = table.Column<string>(type: "TEXT", nullable: false),
                    SozTarihi = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SozTutari = table.Column<decimal>(type: "TEXT", nullable: true),
                    SozDurumu = table.Column<int>(type: "INTEGER", nullable: false),
                    KaydedenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GorusmeNotlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GorusmeNotlari_Mukellefler_MukellefId",
                        column: x => x.MukellefId,
                        principalTable: "Mukellefler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tahakkuklar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MukellefId = table.Column<int>(type: "INTEGER", nullable: false),
                    HizmetTuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    UcretPlaniId = table.Column<int>(type: "INTEGER", nullable: true),
                    DonemBaslangic = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DonemBitis = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false),
                    Aciklama = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Kaynak = table.Column<int>(type: "INTEGER", nullable: false),
                    OlusturanKullaniciId = table.Column<int>(type: "INTEGER", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IptalEdildi = table.Column<bool>(type: "INTEGER", nullable: false),
                    IptalTarihi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IptalEdenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true),
                    IptalNedeni = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tahakkuklar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tahakkuklar_HizmetTurleri_HizmetTuruId",
                        column: x => x.HizmetTuruId,
                        principalTable: "HizmetTurleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tahakkuklar_Mukellefler_MukellefId",
                        column: x => x.MukellefId,
                        principalTable: "Mukellefler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tahsilatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MukellefId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarih = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false),
                    OdemeYontemi = table.Column<int>(type: "INTEGER", nullable: false),
                    TahsilEden = table.Column<string>(type: "TEXT", nullable: true),
                    Hesap = table.Column<string>(type: "TEXT", nullable: true),
                    OdeyenKisi = table.Column<string>(type: "TEXT", nullable: true),
                    DekontDosyasi = table.Column<string>(type: "TEXT", nullable: true),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true),
                    GrupIslemNo = table.Column<Guid>(type: "TEXT", nullable: true),
                    KaydedenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IptalEdildi = table.Column<bool>(type: "INTEGER", nullable: false),
                    IptalTarihi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IptalEdenKullaniciId = table.Column<int>(type: "INTEGER", nullable: true),
                    IptalNedeni = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tahsilatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tahsilatlar_Mukellefler_MukellefId",
                        column: x => x.MukellefId,
                        principalTable: "Mukellefler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UcretPlanlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MukellefId = table.Column<int>(type: "INTEGER", nullable: false),
                    HizmetTuruId = table.Column<int>(type: "INTEGER", nullable: false),
                    Periyot = table.Column<int>(type: "INTEGER", nullable: false),
                    Tutar = table.Column<decimal>(type: "TEXT", nullable: false),
                    GecerlilikBaslangic = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    GecerlilikBitis = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Aciklama = table.Column<string>(type: "TEXT", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UcretPlanlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UcretPlanlari_HizmetTurleri_HizmetTuruId",
                        column: x => x.HizmetTuruId,
                        principalTable: "HizmetTurleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UcretPlanlari_Mukellefler_MukellefId",
                        column: x => x.MukellefId,
                        principalTable: "Mukellefler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "HizmetTurleri",
                columns: new[] { "Id", "Ad", "Aktif", "Sistem" },
                values: new object[,]
                {
                    { 1, "Muhasebe ücreti", true, true },
                    { 2, "Defter tasdiki", true, true },
                    { 3, "Devir (açılış bakiyesi)", true, true },
                    { 4, "Diğer hizmet", true, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GorusmeNotlari_MukellefId",
                table: "GorusmeNotlari",
                column: "MukellefId");

            migrationBuilder.CreateIndex(
                name: "IX_GorusmeNotlari_SozDurumu_SozTarihi",
                table: "GorusmeNotlari",
                columns: new[] { "SozDurumu", "SozTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_IslemGunlugu_Tablo_KayitId",
                table: "IslemGunlugu",
                columns: new[] { "Tablo", "KayitId" });

            migrationBuilder.CreateIndex(
                name: "IX_IslemGunlugu_Tarih",
                table: "IslemGunlugu",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_OdemeGrubuId",
                table: "Mukellefler",
                column: "OdemeGrubuId");

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_Unvan",
                table: "Mukellefler",
                column: "Unvan");

            migrationBuilder.CreateIndex(
                name: "IX_Tahakkuklar_HizmetTuruId",
                table: "Tahakkuklar",
                column: "HizmetTuruId");

            migrationBuilder.CreateIndex(
                name: "IX_Tahakkuklar_MukellefId_Tarih",
                table: "Tahakkuklar",
                columns: new[] { "MukellefId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Tahakkuklar_OtomatikDonem",
                table: "Tahakkuklar",
                columns: new[] { "MukellefId", "HizmetTuruId", "DonemBaslangic" },
                unique: true,
                filter: "Kaynak = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tahsilatlar_GrupIslemNo",
                table: "Tahsilatlar",
                column: "GrupIslemNo");

            migrationBuilder.CreateIndex(
                name: "IX_Tahsilatlar_MukellefId_Tarih",
                table: "Tahsilatlar",
                columns: new[] { "MukellefId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_UcretPlanlari_HizmetTuruId",
                table: "UcretPlanlari",
                column: "HizmetTuruId");

            migrationBuilder.CreateIndex(
                name: "IX_UcretPlanlari_MukellefId_HizmetTuruId_GecerlilikBaslangic",
                table: "UcretPlanlari",
                columns: new[] { "MukellefId", "HizmetTuruId", "GecerlilikBaslangic" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ayarlar");

            migrationBuilder.DropTable(
                name: "GorusmeNotlari");

            migrationBuilder.DropTable(
                name: "IslemGunlugu");

            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Tahakkuklar");

            migrationBuilder.DropTable(
                name: "Tahsilatlar");

            migrationBuilder.DropTable(
                name: "UcretPlanlari");

            migrationBuilder.DropTable(
                name: "HizmetTurleri");

            migrationBuilder.DropTable(
                name: "Mukellefler");

            migrationBuilder.DropTable(
                name: "OdemeGruplari");
        }
    }
}
