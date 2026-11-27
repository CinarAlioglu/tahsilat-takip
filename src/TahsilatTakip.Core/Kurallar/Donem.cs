using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Core.Kurallar;

/// <summary>Dönem (ay / çeyrek / yıl) hesapları.</summary>
public static class Donem
{
    public static readonly string[] AyAdlari =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static DateOnly AyBasi(DateOnly tarih) => new(tarih.Year, tarih.Month, 1);

    public static int AySayisi(UcretPeriyodu periyot) => (int)periyot;

    /// <summary>Verilen ayı içeren dönemin ilk günü. Çeyrekler ve yıllar takvime göredir.</summary>
    public static DateOnly DonemBaslangici(UcretPeriyodu periyot, DateOnly tarih) => periyot switch
    {
        UcretPeriyodu.Aylik => AyBasi(tarih),
        UcretPeriyodu.UcAylik => new DateOnly(tarih.Year, (tarih.Month - 1) / 3 * 3 + 1, 1),
        UcretPeriyodu.Yillik => new DateOnly(tarih.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(periyot)),
    };

    public static DateOnly DonemBitisi(UcretPeriyodu periyot, DateOnly donemBaslangic) =>
        donemBaslangic.AddMonths(AySayisi(periyot)).AddDays(-1);

    /// <summary>Borç, dönem bittikten sonraki ilk gün yazılır (Ekim ücreti → 1 Kasım).</summary>
    public static DateOnly BorcTarihi(UcretPeriyodu periyot, DateOnly donemBaslangic) =>
        donemBaslangic.AddMonths(AySayisi(periyot));

    /// <summary>Planın başlangıç ayı dönem başına denk geliyor mu? (Çeyrek/yıl planları için gerekli.)</summary>
    public static bool DonemBasinaDenkGelir(UcretPeriyodu periyot, DateOnly ay) =>
        ay.Day == 1 && DonemBaslangici(periyot, ay) == ay;

    public static string DonemAdi(UcretPeriyodu periyot, DateOnly donemBaslangic) => periyot switch
    {
        UcretPeriyodu.Aylik => $"{AyAdlari[donemBaslangic.Month - 1]} {donemBaslangic.Year}",
        UcretPeriyodu.UcAylik =>
            $"{AyAdlari[donemBaslangic.Month - 1]}-{AyAdlari[donemBaslangic.Month + 1]} {donemBaslangic.Year}",
        UcretPeriyodu.Yillik => $"{donemBaslangic.Year} yılı",
        _ => donemBaslangic.ToString("MM.yyyy"),
    };

    public static string PeriyotAdi(UcretPeriyodu periyot) => periyot switch
    {
        UcretPeriyodu.Aylik => "Aylık",
        UcretPeriyodu.UcAylik => "3 Aylık",
        UcretPeriyodu.Yillik => "Yıllık",
        _ => periyot.ToString(),
    };
}
