using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Core.Kurallar;

/// <summary>Otomatik oluşturulacak bir borç kaydının önizlemesi.</summary>
public sealed record TahakkukOnerisi(
    int MukellefId,
    int HizmetTuruId,
    int UcretPlaniId,
    UcretPeriyodu Periyot,
    DateOnly DonemBaslangic,
    DateOnly DonemBitis,
    DateOnly Tarih,
    decimal Tutar,
    string Aciklama);

/// <summary>
/// Ücret planlarından, bugüne kadar borç tarihi gelmiş ama henüz yazılmamış dönemleri bulur.
/// Kurallar:
/// <list type="bullet">
/// <item>Borç, dönem bitiminin ertesi günü yazılır (Ekim ücreti 1 Kasım'da).</item>
/// <item>Bir plan, aynı mükellef ve hizmetteki bir sonraki planın başlangıcına kadar geçerlidir.</item>
/// <item>Bir dönem, son ayı planın geçerlilik aralığındaysa o plandan borçlandırılır.</item>
/// <item>Pasif mükelleflere otomatik borç yazılmaz; ayrılanlara ayrılış tarihinden sonra biten dönemler yazılmaz.</item>
/// <item>Daha önce (iptal edilmiş olsa bile) oluşturulmuş otomatik dönemler tekrar oluşturulmaz.</item>
/// </list>
/// </summary>
public static class TahakkukPlanlayici
{
    public static List<TahakkukOnerisi> Hesapla(
        IEnumerable<Mukellef> mukellefler,
        IEnumerable<UcretPlani> planlar,
        IReadOnlyDictionary<int, string> hizmetAdlari,
        ISet<(int MukellefId, int HizmetTuruId, DateOnly DonemBaslangic)> mevcutOtomatikDonemler,
        DateOnly bugun)
    {
        var mukellefSozlugu = mukellefler.ToDictionary(m => m.Id);
        var sonuc = new List<TahakkukOnerisi>();

        foreach (var grup in planlar.GroupBy(p => (p.MukellefId, p.HizmetTuruId)))
        {
            if (!mukellefSozlugu.TryGetValue(grup.Key.MukellefId, out var mukellef)) continue;
            if (mukellef.Durum == MukellefDurumu.Pasif) continue;
            if (mukellef.Durum == MukellefDurumu.Ayrildi && mukellef.AyrilisTarihi is null) continue;

            var sirali = grup.OrderBy(p => p.GecerlilikBaslangic).ThenBy(p => p.Id).ToList();
            for (var i = 0; i < sirali.Count; i++)
            {
                var plan = sirali[i];
                if (plan.Tutar <= 0) continue;

                var baslangic = Donem.AyBasi(plan.GecerlilikBaslangic);
                // Planın kapsadığı son ay (dahil).
                DateOnly? sonAy = plan.GecerlilikBitis is { } bitis ? Donem.AyBasi(bitis) : null;
                if (i + 1 < sirali.Count)
                {
                    var sonrakininOncesi = Donem.AyBasi(sirali[i + 1].GecerlilikBaslangic).AddMonths(-1);
                    sonAy = sonAy is null || sonrakininOncesi < sonAy ? sonrakininOncesi : sonAy;
                }
                if (sonAy < baslangic) continue;

                var hizmetAdi = hizmetAdlari.TryGetValue(plan.HizmetTuruId, out var ad) ? ad : "Ücret";
                var donem = Donem.DonemBaslangici(plan.Periyot, baslangic);

                while (true)
                {
                    var donemBitis = Donem.DonemBitisi(plan.Periyot, donem);
                    var borcTarihi = Donem.BorcTarihi(plan.Periyot, donem);
                    if (borcTarihi > bugun) break;

                    var donemSonAyi = Donem.AyBasi(donemBitis);
                    if (sonAy is not null && donemSonAyi > sonAy) break;
                    if (mukellef.Durum == MukellefDurumu.Ayrildi && donemBitis > mukellef.AyrilisTarihi) break;

                    var dahil = donemSonAyi >= baslangic
                                && !mevcutOtomatikDonemler.Contains((plan.MukellefId, plan.HizmetTuruId, donem));
                    if (dahil)
                    {
                        sonuc.Add(new TahakkukOnerisi(
                            plan.MukellefId, plan.HizmetTuruId, plan.Id, plan.Periyot,
                            donem, donemBitis, borcTarihi, plan.Tutar,
                            $"{Donem.DonemAdi(plan.Periyot, donem)} {hizmetAdi.ToLower(new System.Globalization.CultureInfo("tr-TR"))}"));
                    }

                    donem = donem.AddMonths(Donem.AySayisi(plan.Periyot));
                }
            }
        }

        return sonuc
            .OrderBy(o => o.Tarih)
            .ThenBy(o => mukellefSozlugu[o.MukellefId].Unvan, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
            .ToList();
    }
}
