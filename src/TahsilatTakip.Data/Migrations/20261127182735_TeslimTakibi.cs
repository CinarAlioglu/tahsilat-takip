using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TahsilatTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeslimTakibi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeslimAlan",
                table: "Tahsilatlar",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TeslimEdildi",
                table: "Tahsilatlar",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TeslimTarihi",
                table: "Tahsilatlar",
                type: "TEXT",
                nullable: true);

            // Mevcut kayıtlar: ofis dışında tahsil edilenler "teslim bekliyor", diğerleri teslim edilmiş sayılır.
            migrationBuilder.Sql(
                "UPDATE Tahsilatlar SET TeslimEdildi = CASE WHEN TahsilEden IS NULL OR trim(TahsilEden) = '' " +
                "OR lower(trim(TahsilEden)) = 'ofis' THEN 1 ELSE 0 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeslimAlan",
                table: "Tahsilatlar");

            migrationBuilder.DropColumn(
                name: "TeslimEdildi",
                table: "Tahsilatlar");

            migrationBuilder.DropColumn(
                name: "TeslimTarihi",
                table: "Tahsilatlar");
        }
    }
}
