using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

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
