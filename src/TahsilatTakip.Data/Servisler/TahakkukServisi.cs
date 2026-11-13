using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed record BorcSatiri(
    int Id,
    DateOnly Tarih,
    int MukellefId,
    string Unvan,
    string Hizmet,
    string Aciklama,
    decimal Tutar,
    decimal Kalan,
    TahakkukKaynagi Kaynak,
    bool IptalEdildi,
    string? IptalNedeni);

public sealed record BorcFiltresi(
    DateOnly? Baslangic = null,
    DateOnly? Bitis = null,
    int? MukellefId = null,
    bool SadeceAcik = false,
    bool IptalDahil = false);

/// <summary>Otomatik borç yazma (dönem sonunda, ör. Ekim ücreti 1 Kasım'da).</summary>
public sealed class TahakkukServisi(VeritabaniFabrikasi fabrika)
{
    /// <summary>Bugüne kadar borç tarihi gelmiş ama henüz yazılmamış dönemleri listeler (kayıt yapmaz).</summary>
    public List<TahakkukOnerisi> Onizle(DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var mukellefler = db.Mukellefler.AsNoTracking().ToList();
        var planlar = db.UcretPlanlari.AsNoTracking().ToList();
        var hizmetler = db.HizmetTurleri.AsNoTracking().ToDictionary(h => h.Id, h => h.Ad);
        var mevcut = db.Tahakkuklar.AsNoTracking().Where(t => t.Kaynak == TahakkukKaynagi.Otomatik)
            .Select(t => new { t.MukellefId, t.HizmetTuruId, t.DonemBaslangic }).ToList()
            .Select(t => (t.MukellefId, t.HizmetTuruId, t.DonemBaslangic)).ToHashSet();
        return TahakkukPlanlayici.Hesapla(mukellefler, planlar, hizmetler, mevcut, bugun);
    }

    /// <summary>Önizlemedeki borçları kaydeder. Aynı dönem iki kez yazılmaz.</summary>
    public int Olustur(IEnumerable<TahakkukOnerisi> oneriler, int? kullaniciId)
    {
        using var db = fabrika.Olustur();
        var mevcut = db.Tahakkuklar.Where(t => t.Kaynak == TahakkukKaynagi.Otomatik)
            .Select(t => new { t.MukellefId, t.HizmetTuruId, t.DonemBaslangic }).ToList()
            .Select(t => (t.MukellefId, t.HizmetTuruId, t.DonemBaslangic)).ToHashSet();
        var simdi = DateTime.Now;
        var sayi = 0;
        foreach (var o in oneriler)
        {
            if (!mevcut.Add((o.MukellefId, o.HizmetTuruId, o.DonemBaslangic))) continue;
            db.Tahakkuklar.Add(new Tahakkuk
            {
                MukellefId = o.MukellefId,
                HizmetTuruId = o.HizmetTuruId,
                UcretPlaniId = o.UcretPlaniId,
                DonemBaslangic = o.DonemBaslangic,
                DonemBitis = o.DonemBitis,
                Tarih = o.Tarih,
                Tutar = o.Tutar,
                Aciklama = o.Aciklama,
                Kaynak = TahakkukKaynagi.Otomatik,
                OlusturanKullaniciId = kullaniciId,
                OlusturmaTarihi = simdi,
            });
            sayi++;
        }
        db.SaveChanges();
        return sayi;
    }

    /// <summary>Borç listesi; her borcun ödemelerle kapanmayan kısmı (FIFO) "Kalan" olarak hesaplanır.</summary>
    public List<BorcSatiri> Listele(BorcFiltresi f)
    {
        using var db = fabrika.Olustur();
        var mukellefler = db.Mukellefler.AsNoTracking().ToDictionary(m => m.Id);
        var hizmetler = db.HizmetTurleri.AsNoTracking().ToDictionary(h => h.Id, h => h.Ad);
        var borclarQ = db.Tahakkuklar.AsNoTracking().AsQueryable();
        var odemelerQ = db.Tahsilatlar.AsNoTracking().Where(t => !t.IptalEdildi);
        if (f.MukellefId is { } mid)
        {
            borclarQ = borclarQ.Where(t => t.MukellefId == mid);
            odemelerQ = odemelerQ.Where(t => t.MukellefId == mid);
        }
        var borclar = borclarQ.ToList();
        var odemeler = odemelerQ.ToList().ToLookup(t => t.MukellefId);

        var kalanlar = new Dictionary<int, decimal>();
        foreach (var g in borclar.Where(b => !b.IptalEdildi).GroupBy(b => b.MukellefId))
            foreach (var k in CariHesaplayici.Kapanislar(g, odemeler[g.Key]))
                kalanlar[k.Tahakkuk.Id] = k.Kalan;

        return borclar
            .Where(b => (f.IptalDahil || !b.IptalEdildi)
                        && (f.Baslangic is null || b.Tarih >= f.Baslangic)
                        && (f.Bitis is null || b.Tarih <= f.Bitis))
            .Select(b => new BorcSatiri(b.Id, b.Tarih, b.MukellefId, mukellefler[b.MukellefId].Unvan,
                hizmetler.GetValueOrDefault(b.HizmetTuruId, ""), b.Aciklama, b.Tutar,
                b.IptalEdildi ? 0 : kalanlar.GetValueOrDefault(b.Id), b.Kaynak, b.IptalEdildi, b.IptalNedeni))
            .Where(b => !f.SadeceAcik || b.Kalan > 0)
            .OrderByDescending(b => b.Tarih).ThenBy(b => b.Unvan, Turkce.Karsilastirici)
            .ToList();
    }

    /// <summary>Elle borç (ek hizmet, defter tasdiki vb.).</summary>
    public Tahakkuk ManuelEkle(int mukellefId, int hizmetTuruId, DateOnly tarih, decimal tutar, string aciklama, int? kullaniciId)
    {
        if (tutar <= 0) throw new IslemHatasi("Tutar sıfırdan büyük olmalıdır.");
        if (string.IsNullOrWhiteSpace(aciklama)) throw new IslemHatasi("Açıklama girilmelidir.");
        using var db = fabrika.Olustur();
        var t = new Tahakkuk
        {
            MukellefId = mukellefId,
            HizmetTuruId = hizmetTuruId,
            DonemBaslangic = tarih,
            DonemBitis = tarih,
            Tarih = tarih,
            Tutar = tutar,
            Aciklama = aciklama.Trim(),
            Kaynak = hizmetTuruId == HizmetTuru.DevirId ? TahakkukKaynagi.Devir : TahakkukKaynagi.Manuel,
            OlusturanKullaniciId = kullaniciId,
            OlusturmaTarihi = DateTime.Now,
        };
        db.Tahakkuklar.Add(t);
        db.SaveChanges();
        return t;
    }

    /// <summary>Borç silinmez, gerekçesiyle iptal edilir.</summary>
    public void IptalEt(int tahakkukId, string neden, int? kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(neden)) throw new IslemHatasi("İptal nedeni yazılmalıdır.");
        using var db = fabrika.Olustur();
        var t = db.Tahakkuklar.Find(tahakkukId) ?? throw new IslemHatasi("Kayıt bulunamadı.");
        if (t.IptalEdildi) throw new IslemHatasi("Kayıt zaten iptal edilmiş.");
        t.IptalEdildi = true;
        t.IptalTarihi = DateTime.Now;
        t.IptalEdenKullaniciId = kullaniciId;
        t.IptalNedeni = neden.Trim();
        db.SaveChanges();
    }
}
