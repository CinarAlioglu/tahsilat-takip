using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data;

/// <summary>Uygulamada giriş yapmış kullanıcı.</summary>
public sealed class Oturum
{
    public Kullanici? Kullanici { get; private set; }
    public bool YoneticiMi => Kullanici?.Rol == KullaniciRolu.Yonetici;
    public void Ac(Kullanici k) => Kullanici = k;
    public void Kapat() => Kullanici = null;
}

/// <summary>
/// Veri klasörü: D:\Tahsilat Takip (D: sürücüsü yoksa Belgeler\Tahsilat Takip).
/// TAHSILAT_TAKIP_VERI ortam değişkeniyle değiştirilebilir (geliştirme/test için).
/// </summary>
public sealed class VeriYolu
{
    public const string KlasorAdi = "Tahsilat Takip";

    public VeriYolu(string? kok = null)
    {
        Kok = kok
              ?? Environment.GetEnvironmentVariable("TAHSILAT_TAKIP_VERI")
              ?? VarsayilanKok();
        Directory.CreateDirectory(Kok);
        Directory.CreateDirectory(YedekKlasoru);
        Directory.CreateDirectory(DekontKlasoru);
    }

    public string Kok { get; }
    public string VeritabaniDosyasi => Path.Combine(Kok, "tahsilat.db");
    public string YedekKlasoru => Path.Combine(Kok, "Yedekler");
    public string DekontKlasoru => Path.Combine(Kok, "Dekontlar");
    public string BaglantiCumlesi => $"Data Source={VeritabaniDosyasi};Foreign Keys=True";

    private static string VarsayilanKok()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var d = new DriveInfo("D");
                if (d.IsReady && d.DriveType == DriveType.Fixed) return Path.Combine(@"D:\", KlasorAdi);
            }
            catch (ArgumentException) { /* D: yok */ }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), KlasorAdi);
    }
}

public static class VeritabaniKurulumu
{
    public static DbContextOptions<TahsilatDbContext> Secenekler(string baglantiCumlesi) =>
        new DbContextOptionsBuilder<TahsilatDbContext>().UseSqlite(baglantiCumlesi).Options;

    /// <summary>Veritabanını oluşturur veya yeni sürümün şema değişikliklerini uygular.</summary>
    public static void Hazirla(TahsilatDbContext db)
    {
        db.Database.Migrate();
        // Eşzamanlı okuma/yazma ve çökme dayanıklılığı için WAL kipi.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}

/// <summary>"dotnet ef migrations add" komutunun kullandığı tasarım zamanı fabrikası.</summary>
public sealed class TasarimZamaniFabrikasi : IDesignTimeDbContextFactory<TahsilatDbContext>
{
    public TahsilatDbContext CreateDbContext(string[] args) =>
        new(VeritabaniKurulumu.Secenekler("Data Source=tasarim.db"));
}

/// <summary>Uygulamanın kullandığı DbContext fabrikası (her işlem kendi kısa ömürlü bağlamını açar).</summary>
public sealed class VeritabaniFabrikasi(string baglantiCumlesi, Oturum oturum)
{
    private readonly DbContextOptions<TahsilatDbContext> _secenekler = VeritabaniKurulumu.Secenekler(baglantiCumlesi);
    public TahsilatDbContext Olustur() => new(_secenekler, oturum);
}
