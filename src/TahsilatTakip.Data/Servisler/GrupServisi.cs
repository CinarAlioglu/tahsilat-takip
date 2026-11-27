using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed record GrupUyesi(int MukellefId, string Unvan, decimal Bakiye);

public sealed record GrupSatiri(int Id, string Ad, string? Aciklama, List<GrupUyesi> Uyeler)
{
    public decimal ToplamBakiye => Uyeler.Sum(u => u.Bakiye);
    public string UyeMetni => string.Join(", ", Uyeler.Select(u => u.Unvan));
}

public sealed record GrupDagilimi(int MukellefId, string Unvan, decimal Tutar);

/// <summary>
/// Ödeme grupları: birden çok mükellefin borcunu tek kişi/firma öder (ör. şirket + sahibinin şahıs firması).
/// Gruba gelen ödeme, üyelerin açık borçlarına en eskiden başlayarak dağıtılır; artan kısım ilk üyeye avans yazılır.
/// </summary>
public sealed class GrupServisi(VeritabaniFabrikasi fabrika, TahsilatServisi tahsilatlar)
{
    public List<GrupSatiri> Listele(DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var gruplar = db.OdemeGruplari.AsNoTracking().Include(g => g.Mukellefler).ToList();
        var uyeIdleri = gruplar.SelectMany(g => g.Mukellefler).Select(m => m.Id).ToHashSet();
        var borclar = db.Tahakkuklar.AsNoTracking().Where(t => !t.IptalEdildi && uyeIdleri.Contains(t.MukellefId)).ToList().ToLookup(t => t.MukellefId);
        var odemeler = db.Tahsilatlar.AsNoTracking().Where(t => !t.IptalEdildi && uyeIdleri.Contains(t.MukellefId)).ToList().ToLookup(t => t.MukellefId);
        return gruplar
            .Select(g => new GrupSatiri(g.Id, g.Ad, g.Aciklama, g.Mukellefler
                .OrderBy(m => m.Unvan, Turkce.Karsilastirici)
                .Select(m => new GrupUyesi(m.Id, m.Unvan, CariHesaplayici.Ozet(borclar[m.Id], odemeler[m.Id], bugun).Bakiye))
                .ToList()))
            .OrderBy(g => g.Ad, Turkce.Karsilastirici)
            .ToList();
    }

    public OdemeGrubu Kaydet(int? id, string ad, string? aciklama)
    {
        if (string.IsNullOrWhiteSpace(ad)) throw new IslemHatasi("Grup adı boş olamaz.");
        using var db = fabrika.Olustur();
        var g = id is { } i ? db.OdemeGruplari.Find(i) ?? throw new IslemHatasi("Grup bulunamadı.") : new OdemeGrubu();
        g.Ad = ad.Trim();
        g.Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim();
        if (g.Id == 0) db.OdemeGruplari.Add(g);
        db.SaveChanges();
        return g;
    }

    /// <summary>Grubu siler; üyeler gruptan çıkar ama kayıtları etkilenmez.</summary>
    public void Sil(int grupId)
    {
        using var db = fabrika.Olustur();
        var g = db.OdemeGruplari.Include(x => x.Mukellefler).FirstOrDefault(x => x.Id == grupId)
                ?? throw new IslemHatasi("Grup bulunamadı.");
        foreach (var m in g.Mukellefler) m.OdemeGrubuId = null;
        db.OdemeGruplari.Remove(g);
        db.SaveChanges();
    }

    public void UyeEkle(int grupId, int mukellefId)
    {
        using var db = fabrika.Olustur();
        if (!db.OdemeGruplari.Any(g => g.Id == grupId)) throw new IslemHatasi("Grup bulunamadı.");
        var m = db.Mukellefler.Find(mukellefId) ?? throw new IslemHatasi("Mükellef bulunamadı.");
        if (m.OdemeGrubuId is { } mevcut && mevcut != grupId)
            throw new IslemHatasi($"{m.Unvan} zaten başka bir gruba üye. Önce o gruptan çıkarın.");
        m.OdemeGrubuId = grupId;
        db.SaveChanges();
    }

    public void UyeCikar(int mukellefId)
    {
        using var db = fabrika.Olustur();
        var m = db.Mukellefler.Find(mukellefId) ?? throw new IslemHatasi("Mükellef bulunamadı.");
        m.OdemeGrubuId = null;
        db.SaveChanges();
    }

    /// <summary>Grup ödemesinin üyelere nasıl dağıtılacağını hesaplar (kayıt yapmaz).</summary>
    public List<GrupDagilimi> Dagit(int grupId, decimal tutar)
    {
        if (tutar <= 0) throw new IslemHatasi("Tutar sıfırdan büyük olmalıdır.");
        using var db = fabrika.Olustur();
        var uyeler = db.Mukellefler.AsNoTracking().Where(m => m.OdemeGrubuId == grupId).ToList()
            .OrderBy(m => m.Unvan, Turkce.Karsilastirici).ToList();
        if (uyeler.Count == 0) throw new IslemHatasi("Grupta üye yok.");
        var idler = uyeler.Select(u => u.Id).ToHashSet();
        var borclar = db.Tahakkuklar.AsNoTracking().Where(t => !t.IptalEdildi && idler.Contains(t.MukellefId)).ToList().ToLookup(t => t.MukellefId);
        var odemeler = db.Tahsilatlar.AsNoTracking().Where(t => !t.IptalEdildi && idler.Contains(t.MukellefId)).ToList().ToLookup(t => t.MukellefId);

        // Tüm üyelerin açık borçları tek sırada, en eskiden yeniye.
        var acik = uyeler
            .SelectMany(u => CariHesaplayici.Kapanislar(borclar[u.Id], odemeler[u.Id], u.OdemeGunu)
                .Where(k => !k.Kapandi).Select(k => (Uye: u, k.Tahakkuk.Tarih, k.Tahakkuk.Id, k.Kalan)))
            .OrderBy(x => x.Tarih).ThenBy(x => x.Id);

        var paylar = uyeler.ToDictionary(u => u.Id, _ => 0m);
        var kalan = tutar;
        foreach (var b in acik)
        {
            if (kalan <= 0) break;
            var pay = Math.Min(kalan, b.Kalan);
            paylar[b.Uye.Id] += pay;
            kalan -= pay;
        }
        if (kalan > 0) paylar[uyeler[0].Id] += kalan; // artan: ilk üyeye avans

        return uyeler.Where(u => paylar[u.Id] > 0).Select(u => new GrupDagilimi(u.Id, u.Unvan, paylar[u.Id])).ToList();
    }

    /// <summary>Grup ödemesini kaydeder: her üye için ayrı tahsilat, hepsi aynı işlem numarasıyla bağlı.</summary>
    public List<Tahsilat> TahsilatAl(int grupId, TahsilatGirdisi girdi, int? kullaniciId)
    {
        var dagilim = Dagit(grupId, girdi.Tutar);
        string grupAdi;
        using (var db = fabrika.Olustur())
            grupAdi = db.OdemeGruplari.AsNoTracking().First(g => g.Id == grupId).Ad;

        var islemNo = Guid.NewGuid();
        var aciklama = $"Grup ödemesi: {grupAdi} (toplam {girdi.Tutar.ToString("N2", Turkce.Kultur)} ₺)"
                       + (string.IsNullOrWhiteSpace(girdi.Aciklama) ? "" : $" - {girdi.Aciklama.Trim()}");

        var sonuc = new List<Tahsilat>();
        string? dekont = null;
        foreach (var (pay, i) in dagilim.Select((d, i) => (d, i)))
        {
            // Dekont bir kez kopyalanır, tüm parçalar aynı dosyayı gösterir.
            var t = tahsilatlar.Ekle(girdi with
            {
                MukellefId = pay.MukellefId,
                Tutar = pay.Tutar,
                Aciklama = aciklama,
                DekontKaynakDosyasi = i == 0 ? girdi.DekontKaynakDosyasi : null,
            }, kullaniciId);
            dekont ??= t.DekontDosyasi;
            sonuc.Add(t);
        }

        using (var db = fabrika.Olustur())
        {
            var idler = sonuc.Select(t => t.Id).ToHashSet();
            foreach (var t in db.Tahsilatlar.Where(t => idler.Contains(t.Id)))
            {
                t.GrupIslemNo = islemNo;
                t.DekontDosyasi ??= dekont;
            }
            db.SaveChanges();
        }
        return sonuc;
    }
}
