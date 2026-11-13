using Microsoft.EntityFrameworkCore;

namespace TahsilatTakip.Data.Servisler;

/// <summary>
/// Veritabanının tutarlı bir kopyasını Yedekler klasörüne alır (SQLite VACUUM INTO).
/// Son <see cref="SaklanacakYedek"/> yedek tutulur, eskiler silinir.
/// </summary>
public sealed class YedekServisi(VeritabaniFabrikasi fabrika, VeriYolu yol)
{
    public const int SaklanacakYedek = 30;
    public const string EkKlasorAyari = "EkYedekKlasoru";

    public string Yedekle(string? hedefKlasor = null)
    {
        hedefKlasor ??= yol.YedekKlasoru;
        Directory.CreateDirectory(hedefKlasor);
        var dosya = Path.Combine(hedefKlasor, $"tahsilat_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        using (var db = fabrika.Olustur())
        {
            db.Database.ExecuteSql($"VACUUM INTO {dosya}");
        }
        EskileriTemizle(hedefKlasor);
        return dosya;
    }

    /// <summary>Uygulama kapanırken: yerel yedek + tanımlıysa ek klasöre (ör. Google Drive) kopya.</summary>
    public List<string> KapanisYedegi()
    {
        var sonuc = new List<string> { Yedekle() };
        var ek = EkKlasor();
        if (!string.IsNullOrWhiteSpace(ek))
        {
            try { sonuc.Add(Yedekle(ek)); }
            catch (Exception) { /* Ek klasöre ulaşılamazsa yerel yedek yeterli. */ }
        }
        return sonuc;
    }

    public string? EkKlasor()
    {
        using var db = fabrika.Olustur();
        return db.Ayarlar.Find(EkKlasorAyari)?.Deger;
    }

    public void EkKlasorKaydet(string? klasor)
    {
        using var db = fabrika.Olustur();
        var ayar = db.Ayarlar.Find(EkKlasorAyari);
        if (ayar is null) db.Ayarlar.Add(new Core.Varliklar.Ayar { Anahtar = EkKlasorAyari, Deger = klasor });
        else ayar.Deger = klasor;
        db.SaveChanges();
    }

    public DateTime? SonYedekTarihi() =>
        Directory.Exists(yol.YedekKlasoru)
            ? Directory.GetFiles(yol.YedekKlasoru, "tahsilat_*.db").Select(File.GetLastWriteTime)
                .DefaultIfEmpty().Max() is var t && t != default ? t : null
            : null;

    private static void EskileriTemizle(string klasor)
    {
        foreach (var eski in Directory.GetFiles(klasor, "tahsilat_*.db")
                     .OrderByDescending(f => f).Skip(SaklanacakYedek))
        {
            try { File.Delete(eski); } catch (IOException) { }
        }
    }
}
