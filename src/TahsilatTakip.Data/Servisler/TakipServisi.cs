using System.Text;
using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

/// <summary>Gecikenler listesinde bir satır; açık borçlar vadesine göre yaş dilimlerine ayrılır.</summary>
public sealed record GecikenSatiri(
    int Id,
    string Unvan,
    string? Telefon,
    decimal Bakiye,
    int GecikmeGunu,
    decimal Gun0_30,
    decimal Gun31_60,
    decimal Gun61_90,
    decimal Gun90Ustu,
    int AcikBorcSayisi,
    string? OdemeGrubu,
    DateTime? SonGorusme,
    string? SonGorusmeNotu,
    DateOnly? AcikSozTarihi,
    SozDurumu? AcikSozDurumu)
{
    public string SozMetni => AcikSozTarihi is { } t
        ? (AcikSozDurumu == SozDurumu.Tutulmadi ? $"Tutulmadı ({t:dd.MM})" : $"{t:dd.MM.yyyy}")
        : "";
}

public enum AyDurumu { Yok, Odendi, Kismi, Acik }

public sealed class AylikDurumSatiri
{
    public int Id { get; init; }
    public string Unvan { get; init; } = "";
    public string UcretBilgisi { get; init; } = "";
    /// <summary>Yıl başından önceki dönemlerden kalan (devir dahil).</summary>
    public decimal OncekiKalan { get; init; }
    public AyDurumu[] Aylar { get; } = new AyDurumu[12];
    public decimal[] AyKalanlari { get; } = new decimal[12];
    public decimal Bakiye { get; init; }

    // WPF tablo bağlaması için ay sütunları.
    public AyDurumu A1 => Aylar[0]; public AyDurumu A2 => Aylar[1]; public AyDurumu A3 => Aylar[2];
    public AyDurumu A4 => Aylar[3]; public AyDurumu A5 => Aylar[4]; public AyDurumu A6 => Aylar[5];
    public AyDurumu A7 => Aylar[6]; public AyDurumu A8 => Aylar[7]; public AyDurumu A9 => Aylar[8];
    public AyDurumu A10 => Aylar[9]; public AyDurumu A11 => Aylar[10]; public AyDurumu A12 => Aylar[11];
}

/// <summary>Gecikenler, hatırlatma mesajı ve Excel'deki gibi aylık ödeme durum tablosu.</summary>
public sealed class TakipServisi(VeritabaniFabrikasi fabrika, AyarServisi ayarlar)
{
    private sealed record Veri(
        List<Mukellef> Mukellefler,
        ILookup<int, Tahakkuk> Borclar,
        ILookup<int, Tahsilat> Odemeler,
        ILookup<int, UcretPlani> Planlar);

    private Veri Yukle(TahsilatDbContext db) => new(
        db.Mukellefler.AsNoTracking().Include(m => m.OdemeGrubu).ToList(),
        db.Tahakkuklar.AsNoTracking().Where(t => !t.IptalEdildi).ToList().ToLookup(t => t.MukellefId),
        db.Tahsilatlar.AsNoTracking().Where(t => !t.IptalEdildi).ToList().ToLookup(t => t.MukellefId),
        db.UcretPlanlari.AsNoTracking().ToList().ToLookup(p => p.MukellefId));

    public List<GecikenSatiri> Gecikenler(DateOnly bugun, bool sadeceGecikmis = true)
    {
        using var db = fabrika.Olustur();
        var v = Yukle(db);
        var gorusmeler = db.GorusmeNotlari.AsNoTracking().ToList().ToLookup(g => g.MukellefId);

        var sonuc = new List<GecikenSatiri>();
        foreach (var m in v.Mukellefler)
        {
            var ozet = CariHesaplayici.Ozet(v.Borclar[m.Id], v.Odemeler[m.Id], bugun, m.OdemeGunu);
            if (ozet.Bakiye <= 0 || (sadeceGecikmis && ozet.GecikmeGunu <= 0)) continue;

            decimal d1 = 0, d2 = 0, d3 = 0, d4 = 0;
            foreach (var k in CariHesaplayici.Kapanislar(v.Borclar[m.Id], v.Odemeler[m.Id], m.OdemeGunu).Where(k => !k.Kapandi))
            {
                var gun = bugun.DayNumber - k.Vade.DayNumber;
                if (gun <= 30) d1 += k.Kalan;
                else if (gun <= 60) d2 += k.Kalan;
                else if (gun <= 90) d3 += k.Kalan;
                else d4 += k.Kalan;
            }

            var son = gorusmeler[m.Id].OrderByDescending(g => g.Tarih).FirstOrDefault();
            var soz = gorusmeler[m.Id].Where(g => g.SozDurumu is SozDurumu.Bekliyor or SozDurumu.Tutulmadi)
                .OrderByDescending(g => g.Tarih).FirstOrDefault();
            if (soz is not null && son is not null && son.Tarih > soz.Tarih && soz.SozDurumu == SozDurumu.Tutulmadi) soz = null;

            sonuc.Add(new GecikenSatiri(m.Id, m.Unvan, m.OdemeSorumlusuTelefon ?? m.Telefon, ozet.Bakiye, ozet.GecikmeGunu,
                d1, d2, d3, d4, ozet.AcikBorcSayisi, m.OdemeGrubu?.Ad,
                son?.Tarih, son?.Not, soz?.SozTarihi, soz?.SozDurumu));
        }
        return sonuc.OrderByDescending(s => s.GecikmeGunu).ThenByDescending(s => s.Bakiye).ToList();
    }

    /// <summary>WhatsApp/SMS ile gönderilecek hatırlatma metni.</summary>
    public string HatirlatmaMesaji(int mukellefId, DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var m = db.Mukellefler.AsNoTracking().First(x => x.Id == mukellefId);
        var borclar = db.Tahakkuklar.AsNoTracking().Where(t => t.MukellefId == mukellefId && !t.IptalEdildi).ToList();
        var odemeler = db.Tahsilatlar.AsNoTracking().Where(t => t.MukellefId == mukellefId && !t.IptalEdildi).ToList();
        var ozet = CariHesaplayici.Ozet(borclar, odemeler, bugun, m.OdemeGunu);
        var acik = CariHesaplayici.Kapanislar(borclar, odemeler, m.OdemeGunu).Where(k => !k.Kapandi).ToList();

        var tr = Turkce.Kultur;
        var sb = new StringBuilder();
        sb.AppendLine($"Sayın {m.OdemeSorumlusuAd ?? m.Unvan},");
        sb.AppendLine();
        if (ozet.Bakiye <= 0)
        {
            sb.AppendLine($"{bugun:dd.MM.yyyy} itibarıyla muhasebe hizmet bedeli borcunuz bulunmamaktadır. Teşekkür ederiz.");
        }
        else
        {
            sb.AppendLine($"{bugun:dd.MM.yyyy} itibarıyla muhasebe hizmet bedeli bakiyeniz {ozet.Bakiye.ToString("N2", tr)} TL'dir.");
            if (acik.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Ödenmemiş dönemler:");
                foreach (var k in acik)
                    sb.AppendLine($"- {k.Tahakkuk.Aciklama}: {k.Kalan.ToString("N2", tr)} TL");
            }
            sb.AppendLine();
            sb.AppendLine("Ödemenizi en kısa sürede yapmanızı rica ederiz.");
            if (ayarlar.Oku(AyarServisi.MesajIban) is { } iban) sb.AppendLine($"IBAN: {iban}");
        }
        sb.AppendLine();
        sb.Append("Saygılarımızla");
        if (ayarlar.Oku(AyarServisi.OfisAdi) is { } ofis) sb.Append($",\n{ofis}");
        if (ayarlar.Oku(AyarServisi.OfisTelefonu) is { } tel) sb.Append($"\n{tel}");
        return sb.ToString();
    }

    /// <summary>
    /// Seçilen yılın muhasebe ücreti dönemlerinin ödeme durumu (Excel'deki X tablosu gibi).
    /// 3 aylık ve yıllık ücretlerde dönemin tüm ayları aynı durumu gösterir.
    /// </summary>
    public List<AylikDurumSatiri> AylikDurum(int yil, DateOnly bugun, bool ayrilanlarDahil = false)
    {
        using var db = fabrika.Olustur();
        var v = Yukle(db);
        var yilBasi = new DateOnly(yil, 1, 1);
        var sonuc = new List<AylikDurumSatiri>();

        foreach (var m in v.Mukellefler.Where(m => ayrilanlarDahil || m.Durum != MukellefDurumu.Ayrildi))
        {
            var kapanislar = CariHesaplayici.Kapanislar(v.Borclar[m.Id], v.Odemeler[m.Id], m.OdemeGunu);
            var satir = new AylikDurumSatiri
            {
                Id = m.Id,
                Unvan = m.Unvan,
                UcretBilgisi = UcretBilgisi(v.Planlar[m.Id], bugun),
                OncekiKalan = kapanislar.Where(k => k.Tahakkuk.DonemBaslangic < yilBasi).Sum(k => k.Kalan),
                Bakiye = CariHesaplayici.Ozet(v.Borclar[m.Id], v.Odemeler[m.Id], bugun).Bakiye,
            };
            foreach (var k in kapanislar.Where(k => k.Tahakkuk.HizmetTuruId == HizmetTuru.AylikMuhasebeUcretiId
                                                     && k.Tahakkuk.Kaynak != TahakkukKaynagi.Devir))
            {
                var t = k.Tahakkuk;
                var durum = k.Kalan <= 0 ? AyDurumu.Odendi : k.Odenen > 0 ? AyDurumu.Kismi : AyDurumu.Acik;
                for (var ay = t.DonemBaslangic; ay <= t.DonemBitis; ay = ay.AddMonths(1))
                {
                    if (ay.Year != yil) continue;
                    var i = ay.Month - 1;
                    // Aynı aya birden çok borç düştüyse en kötü durum gösterilir.
                    if (satir.Aylar[i] == AyDurumu.Yok || durum > satir.Aylar[i]) satir.Aylar[i] = durum;
                    satir.AyKalanlari[i] += k.Kalan / Math.Max(1, (t.DonemBitis.Year - t.DonemBaslangic.Year) * 12 + t.DonemBitis.Month - t.DonemBaslangic.Month + 1);
                }
            }
            sonuc.Add(satir);
        }
        return sonuc.OrderBy(s => s.Unvan, Turkce.Karsilastirici).ToList();
    }

    private static string UcretBilgisi(IEnumerable<UcretPlani> planlar, DateOnly bugun)
    {
        var ay = Donem.AyBasi(bugun);
        var p = planlar.Where(p => p.HizmetTuruId == HizmetTuru.AylikMuhasebeUcretiId && p.GecerlilikBaslangic <= ay)
                    .OrderByDescending(p => p.GecerlilikBaslangic).FirstOrDefault()
                ?? planlar.Where(p => p.HizmetTuruId == HizmetTuru.AylikMuhasebeUcretiId)
                    .OrderBy(p => p.GecerlilikBaslangic).FirstOrDefault();
        return p is null ? "" : $"{Donem.PeriyotAdi(p.Periyot)} {p.Tutar.ToString("N0", Turkce.Kultur)}";
    }
}
