using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Core.Kurallar;

public enum EkstreSatirTuru { Borc, Odeme }

public sealed record EkstreSatiri(
    DateOnly Tarih,
    EkstreSatirTuru Tur,
    string Aciklama,
    decimal Borc,
    decimal Odeme,
    decimal Bakiye,
    int KayitId);

/// <summary>Bir borç kaydının ödemelerle ne kadar kapandığı.</summary>
public sealed record BorcKapanisi(Tahakkuk Tahakkuk, decimal Odenen, DateOnly Vade)
{
    public decimal Kalan => Tahakkuk.Tutar - Odenen;
    public bool Kapandi => Kalan <= 0;
}

public sealed record CariOzet(
    decimal ToplamBorc,
    decimal ToplamOdeme,
    decimal Bakiye,
    decimal Avans,
    DateOnly? EnEskiAcikVade,
    int GecikmeGunu,
    int AcikBorcSayisi);

/// <summary>
/// Cari hesap hesapları. Bakiye her zaman borç ve ödemelerden hesaplanır, ayrıca saklanmaz.
/// Ödemeler borçları en eskiden başlayarak kapatır (FIFO).
/// </summary>
public static class CariHesaplayici
{
    public static List<EkstreSatiri> Ekstre(IEnumerable<Tahakkuk> tahakkuklar, IEnumerable<Tahsilat> tahsilatlar)
    {
        var satirlar = tahakkuklar.Where(t => !t.IptalEdildi)
            .Select(t => (t.Tarih, Sira: 0, t.Id, Tur: EkstreSatirTuru.Borc, t.Aciklama, Borc: t.Tutar, Odeme: 0m))
            .Concat(tahsilatlar.Where(t => !t.IptalEdildi)
                .Select(t => (t.Tarih, Sira: 1, t.Id, Tur: EkstreSatirTuru.Odeme,
                    Aciklama: OdemeAciklamasi(t), Borc: 0m, Odeme: t.Tutar)))
            .OrderBy(s => s.Tarih).ThenBy(s => s.Sira).ThenBy(s => s.Id);

        var bakiye = 0m;
        var sonuc = new List<EkstreSatiri>();
        foreach (var s in satirlar)
        {
            bakiye += s.Borc - s.Odeme;
            sonuc.Add(new EkstreSatiri(s.Tarih, s.Tur, s.Aciklama, s.Borc, s.Odeme, bakiye, s.Id));
        }
        return sonuc;
    }

    public static List<BorcKapanisi> Kapanislar(
        IEnumerable<Tahakkuk> tahakkuklar, IEnumerable<Tahsilat> tahsilatlar, int? odemeGunu = null)
    {
        var kalanOdeme = tahsilatlar.Where(t => !t.IptalEdildi).Sum(t => t.Tutar);
        var sonuc = new List<BorcKapanisi>();
        foreach (var t in tahakkuklar.Where(t => !t.IptalEdildi)
                     .OrderBy(t => t.Tarih).ThenBy(t => t.DonemBaslangic).ThenBy(t => t.Id))
        {
            var odenen = Math.Min(Math.Max(kalanOdeme, 0), t.Tutar);
            kalanOdeme -= odenen;
            sonuc.Add(new BorcKapanisi(t, odenen, Vade(t.Tarih, odemeGunu)));
        }
        return sonuc;
    }

    public static CariOzet Ozet(
        IEnumerable<Tahakkuk> tahakkuklar, IEnumerable<Tahsilat> tahsilatlar, DateOnly bugun, int? odemeGunu = null)
    {
        var borclar = tahakkuklar.Where(t => !t.IptalEdildi).ToList();
        var odemeler = tahsilatlar.Where(t => !t.IptalEdildi).ToList();
        var toplamBorc = borclar.Sum(t => t.Tutar);
        var toplamOdeme = odemeler.Sum(t => t.Tutar);
        var bakiye = toplamBorc - toplamOdeme;

        var acik = Kapanislar(borclar, odemeler, odemeGunu).Where(k => !k.Kapandi).ToList();
        DateOnly? enEski = acik.Count > 0 ? acik.Min(k => k.Vade) : null;
        var gecikme = enEski is { } v && v < bugun ? bugun.DayNumber - v.DayNumber : 0;

        return new CariOzet(toplamBorc, toplamOdeme, bakiye, bakiye < 0 ? -bakiye : 0, enEski, gecikme, acik.Count);
    }

    /// <summary>
    /// Borcun vadesi: mükellefin ödeme günü tanımlıysa borç ayının o günü, değilse borç tarihi.
    /// </summary>
    public static DateOnly Vade(DateOnly borcTarihi, int? odemeGunu)
    {
        if (odemeGunu is not { } gun || gun < 1) return borcTarihi;
        var sonGun = DateTime.DaysInMonth(borcTarihi.Year, borcTarihi.Month);
        return new DateOnly(borcTarihi.Year, borcTarihi.Month, Math.Min(gun, sonGun));
    }

    private static string OdemeAciklamasi(Tahsilat t)
    {
        var yontem = t.OdemeYontemi switch
        {
            OdemeYontemi.Nakit => "Nakit",
            OdemeYontemi.HavaleEft => "Havale/EFT",
            OdemeYontemi.KrediKarti => "Kredi kartı",
            OdemeYontemi.CekSenet => "Çek/Senet",
            _ => "Ödeme",
        };
        var parcalar = new List<string> { $"Ödeme ({yontem})" };
        if (!string.IsNullOrWhiteSpace(t.TahsilEden)) parcalar.Add($"tahsil eden: {t.TahsilEden}");
        if (!string.IsNullOrWhiteSpace(t.Aciklama)) parcalar.Add(t.Aciklama!);
        return string.Join(" - ", parcalar);
    }
}
