using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed record GorusmeSatiri(
    int Id,
    DateTime Tarih,
    int MukellefId,
    string Unvan,
    GorusmeKanali Kanal,
    string Not,
    DateOnly? SozTarihi,
    decimal? SozTutari,
    SozDurumu SozDurumu,
    string? Kaydeden)
{
    public string SozMetni => SozDurumu == SozDurumu.Yok || SozTarihi is null
        ? ""
        : $"{SozTarihi:dd.MM.yyyy}" + (SozTutari is { } t ? $" · {t.ToString("N2", Turkce.Kultur)} ₺" : "");
}

/// <summary>
/// Mükellefle yapılan görüşmeler ve verilen ödeme sözleri.
/// Bekleyen bir söz, görüşmeden sonra yapılan ödemeler söz tutarına (tutar yoksa herhangi bir ödemeye)
/// ulaşınca "Tutuldu", söz tarihi geçtiği halde ulaşmazsa "Tutulmadı" olur.
/// </summary>
public sealed class GorusmeServisi(VeritabaniFabrikasi fabrika)
{
    public GorusmeNotu Ekle(int mukellefId, DateTime tarih, GorusmeKanali kanal, string not,
        DateOnly? sozTarihi, decimal? sozTutari, int? kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(not)) throw new IslemHatasi("Görüşme notu boş olamaz.");
        if (sozTutari is <= 0) throw new IslemHatasi("Söz tutarı sıfırdan büyük olmalıdır.");
        if (sozTutari is not null && sozTarihi is null) throw new IslemHatasi("Söz tutarı girildiyse söz tarihi de seçilmelidir.");

        using var db = fabrika.Olustur();
        if (!db.Mukellefler.Any(m => m.Id == mukellefId)) throw new IslemHatasi("Mükellef bulunamadı.");

        // Yeni söz verildiyse aynı mükellefin önceki bekleyen sözü geçersiz kalır.
        if (sozTarihi is not null)
        {
            foreach (var eski in db.GorusmeNotlari.Where(g => g.MukellefId == mukellefId && g.SozDurumu == SozDurumu.Bekliyor))
                eski.SozDurumu = SozDurumu.Tutulmadi;
        }

        var g = new GorusmeNotu
        {
            MukellefId = mukellefId,
            Tarih = tarih,
            Kanal = kanal,
            Not = not.Trim(),
            SozTarihi = sozTarihi,
            SozTutari = sozTutari,
            SozDurumu = sozTarihi is null ? SozDurumu.Yok : SozDurumu.Bekliyor,
            KaydedenKullaniciId = kullaniciId,
        };
        db.GorusmeNotlari.Add(g);
        db.SaveChanges();
        return g;
    }

    /// <summary>Bekleyen sözlerin durumunu ödemelere ve bugünün tarihine göre günceller.</summary>
    public void SozleriGuncelle(DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var bekleyenler = db.GorusmeNotlari.Where(g => g.SozDurumu == SozDurumu.Bekliyor).ToList();
        if (bekleyenler.Count == 0) return;
        var mukellefIdleri = bekleyenler.Select(b => b.MukellefId).ToHashSet();
        var odemeler = db.Tahsilatlar.AsNoTracking()
            .Where(t => !t.IptalEdildi && mukellefIdleri.Contains(t.MukellefId)).ToList()
            .ToLookup(t => t.MukellefId);

        foreach (var s in bekleyenler)
        {
            var baslangic = DateOnly.FromDateTime(s.Tarih);
            var odenen = odemeler[s.MukellefId].Where(t => t.Tarih >= baslangic).Sum(t => t.Tutar);
            if (odenen > 0 && odenen >= (s.SozTutari ?? 0)) s.SozDurumu = SozDurumu.Tutuldu;
            else if (s.SozTarihi < bugun) s.SozDurumu = SozDurumu.Tutulmadi;
        }
        db.SaveChanges();
    }

    public List<GorusmeSatiri> Listele(int? mukellefId = null, bool sadeceSozler = false)
    {
        using var db = fabrika.Olustur();
        var q = db.GorusmeNotlari.AsNoTracking().Include(g => g.Mukellef).AsQueryable();
        if (mukellefId is { } m) q = q.Where(g => g.MukellefId == m);
        if (sadeceSozler) q = q.Where(g => g.SozDurumu != SozDurumu.Yok);
        var kullanicilar = db.Kullanicilar.AsNoTracking().ToDictionary(k => k.Id, k => k.AdSoyad);
        return q.ToList()
            .OrderByDescending(g => g.Tarih).ThenByDescending(g => g.Id)
            .Select(g => Satir(g, kullanicilar))
            .ToList();
    }

    /// <summary>
    /// Takip edilmesi gereken sözler: bekleyenler ve tutulmamış olup sonrasında mükellefle yeniden
    /// görüşülmemiş olanlar.
    /// </summary>
    public List<GorusmeSatiri> TakiptekiSozler()
    {
        using var db = fabrika.Olustur();
        var tumu = db.GorusmeNotlari.AsNoTracking().Include(g => g.Mukellef).ToList();
        var sonGorusme = tumu.GroupBy(g => g.MukellefId).ToDictionary(x => x.Key, x => x.Max(g => g.Tarih));
        var kullanicilar = db.Kullanicilar.AsNoTracking().ToDictionary(k => k.Id, k => k.AdSoyad);
        return tumu
            .Where(g => g.SozDurumu == SozDurumu.Bekliyor
                        || (g.SozDurumu == SozDurumu.Tutulmadi && sonGorusme[g.MukellefId] <= g.Tarih))
            .OrderBy(g => g.SozTarihi)
            .Select(g => Satir(g, kullanicilar))
            .ToList();
    }

    public void SozDurumuDegistir(int gorusmeId, SozDurumu durum)
    {
        using var db = fabrika.Olustur();
        var g = db.GorusmeNotlari.Find(gorusmeId) ?? throw new IslemHatasi("Görüşme bulunamadı.");
        if (g.SozDurumu == SozDurumu.Yok) throw new IslemHatasi("Bu görüşmede ödeme sözü yok.");
        g.SozDurumu = durum;
        db.SaveChanges();
    }

    private static GorusmeSatiri Satir(GorusmeNotu g, Dictionary<int, string> kullanicilar) =>
        new(g.Id, g.Tarih, g.MukellefId, g.Mukellef!.Unvan, g.Kanal, g.Not, g.SozTarihi, g.SozTutari, g.SozDurumu,
            g.KaydedenKullaniciId is { } k ? kullanicilar.GetValueOrDefault(k) : null);
}
