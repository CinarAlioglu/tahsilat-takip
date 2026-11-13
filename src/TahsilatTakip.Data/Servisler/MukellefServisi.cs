using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

/// <summary>Mükellef listesinde bir satır.</summary>
public sealed record MukellefOzeti(
    int Id,
    string Unvan,
    MukellefDurumu Durum,
    string UcretBilgisi,
    decimal Bakiye,
    int GecikmeGunu,
    string? Telefon,
    OdemeTercihi OdemeTercihi,
    string? OdemeGrubu);

public sealed class MukellefServisi(VeritabaniFabrikasi fabrika)
{
    public List<MukellefOzeti> Listele(DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var mukellefler = db.Mukellefler.AsNoTracking().Include(m => m.OdemeGrubu).ToList();
        var planlar = db.UcretPlanlari.AsNoTracking().ToList().ToLookup(p => p.MukellefId);
        var borclar = db.Tahakkuklar.AsNoTracking().Where(t => !t.IptalEdildi).ToList().ToLookup(t => t.MukellefId);
        var odemeler = db.Tahsilatlar.AsNoTracking().Where(t => !t.IptalEdildi).ToList().ToLookup(t => t.MukellefId);

        return mukellefler
            .Select(m =>
            {
                var ozet = CariHesaplayici.Ozet(borclar[m.Id], odemeler[m.Id], bugun, m.OdemeGunu);
                return new MukellefOzeti(m.Id, m.Unvan, m.Durum, UcretBilgisi(planlar[m.Id], bugun),
                    ozet.Bakiye, ozet.GecikmeGunu, m.Telefon, m.OdemeTercihi, m.OdemeGrubu?.Ad);
            })
            .OrderBy(m => m.Unvan, Turkce.Karsilastirici)
            .ToList();
    }

    /// <summary>Bugün geçerli muhasebe ücreti planının kısa açıklaması, ör. "Aylık 6.000 ₺".</summary>
    private static string UcretBilgisi(IEnumerable<UcretPlani> planlar, DateOnly bugun)
    {
        var ay = Donem.AyBasi(bugun);
        var gecerli = planlar
            .Where(p => p.HizmetTuruId == HizmetTuru.AylikMuhasebeUcretiId && p.GecerlilikBaslangic <= ay
                        && (p.GecerlilikBitis is null || p.GecerlilikBitis >= ay))
            .OrderByDescending(p => p.GecerlilikBaslangic)
            .FirstOrDefault()
            // Henüz başlamamış (gelecek ay başlayacak) plan varsa onu göster.
            ?? planlar.Where(p => p.HizmetTuruId == HizmetTuru.AylikMuhasebeUcretiId)
                .OrderBy(p => p.GecerlilikBaslangic).FirstOrDefault(p => p.GecerlilikBaslangic > ay);
        return gecerli is null ? "—" : $"{Donem.PeriyotAdi(gecerli.Periyot)} {gecerli.Tutar.ToString("N0", Turkce.Kultur)} ₺";
    }

    public Mukellef? Getir(int id)
    {
        using var db = fabrika.Olustur();
        return db.Mukellefler.AsNoTracking().FirstOrDefault(m => m.Id == id);
    }

    public Mukellef Kaydet(Mukellef m)
    {
        m.Unvan = (m.Unvan ?? "").Trim();
        if (m.Unvan.Length == 0) throw new IslemHatasi("Ünvan boş olamaz.");
        if (m.OdemeGunu is < 1 or > 31) throw new IslemHatasi("Ödeme günü 1 ile 31 arasında olmalıdır.");
        if (!string.IsNullOrWhiteSpace(m.VknTckn) && m.VknTckn.Trim() is var vkn
            && (!vkn.All(char.IsDigit) || vkn.Length is not (10 or 11)))
            throw new IslemHatasi("VKN 10, TCKN 11 haneli olmalıdır.");
        if (m.Durum == MukellefDurumu.Ayrildi && m.AyrilisTarihi is null)
            throw new IslemHatasi("Ayrılan mükellef için ayrılış tarihi girilmelidir.");
        if (m.Durum != MukellefDurumu.Ayrildi) m.AyrilisTarihi = null;

        using var db = fabrika.Olustur();
        var digerleri = db.Mukellefler.Where(x => x.Id != m.Id).Select(x => x.Unvan).ToList();
        if (digerleri.Any(u => Turkce.Karsilastirici.Equals(u.Trim(), m.Unvan)))
            throw new IslemHatasi("Bu ünvanla kayıtlı başka bir mükellef var.");

        if (m.Id == 0)
        {
            m.OlusturmaTarihi = DateTime.Now;
            db.Mukellefler.Add(m);
        }
        else
        {
            var mevcut = db.Mukellefler.Find(m.Id) ?? throw new IslemHatasi("Mükellef bulunamadı.");
            var olusturma = mevcut.OlusturmaTarihi;
            db.Entry(mevcut).CurrentValues.SetValues(m);
            mevcut.OlusturmaTarihi = olusturma;
        }
        db.SaveChanges();
        return m;
    }

    // ---- Ücret planları -----------------------------------------------------------------

    public List<UcretPlani> Planlar(int mukellefId)
    {
        using var db = fabrika.Olustur();
        return db.UcretPlanlari.AsNoTracking().Include(p => p.HizmetTuru)
            .Where(p => p.MukellefId == mukellefId).ToList()
            .OrderBy(p => p.HizmetTuruId).ThenByDescending(p => p.GecerlilikBaslangic).ToList();
    }

    public List<HizmetTuru> HizmetTurleri(bool devirDahil = false)
    {
        using var db = fabrika.Olustur();
        return db.HizmetTurleri.AsNoTracking().Where(h => h.Aktif && (devirDahil || h.Id != HizmetTuru.DevirId))
            .OrderBy(h => h.Id).ToList();
    }

    /// <summary>
    /// Yeni ücret planı ekler (zam veya yeni hizmet). Önceki plan bu planın başlangıcına kadar geçerli kalır.
    /// </summary>
    public UcretPlani PlanEkle(int mukellefId, int hizmetTuruId, UcretPeriyodu periyot, decimal tutar,
        DateOnly baslangicAyi, string? aciklama = null)
    {
        if (hizmetTuruId == HizmetTuru.DevirId) throw new IslemHatasi("Devir için ücret planı tanımlanamaz.");
        if (tutar <= 0) throw new IslemHatasi("Tutar sıfırdan büyük olmalıdır.");
        baslangicAyi = Donem.AyBasi(baslangicAyi);
        if (!Donem.DonemBasinaDenkGelir(periyot, baslangicAyi))
            throw new IslemHatasi(periyot == UcretPeriyodu.UcAylik
                ? "3 aylık planlar çeyrek başında (Ocak, Nisan, Temmuz, Ekim) başlamalıdır."
                : "Yıllık planlar Ocak ayında başlamalıdır.");

        using var db = fabrika.Olustur();
        if (!db.Mukellefler.Any(m => m.Id == mukellefId)) throw new IslemHatasi("Mükellef bulunamadı.");
        if (db.UcretPlanlari.Any(p => p.MukellefId == mukellefId && p.HizmetTuruId == hizmetTuruId
                                      && p.GecerlilikBaslangic == baslangicAyi))
            throw new IslemHatasi("Bu hizmet için aynı aydan başlayan bir plan zaten var.");

        // Bu tarihten sonraki dönemler için zaten otomatik borç yazıldıysa uyar.
        var yazilmis = db.Tahakkuklar.Where(t => t.MukellefId == mukellefId && t.HizmetTuruId == hizmetTuruId
                                                 && t.Kaynak == TahakkukKaynagi.Otomatik && !t.IptalEdildi)
            .Select(t => t.DonemBaslangic).ToList();
        if (yazilmis.Any(d => d >= baslangicAyi))
            throw new IslemHatasi("Bu tarihten sonraki dönemler için borç zaten yazılmış. Önce o borçları iptal edin.");

        var plan = new UcretPlani
        {
            MukellefId = mukellefId,
            HizmetTuruId = hizmetTuruId,
            Periyot = periyot,
            Tutar = tutar,
            GecerlilikBaslangic = baslangicAyi,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim(),
            OlusturmaTarihi = DateTime.Now,
        };
        db.UcretPlanlari.Add(plan);
        db.SaveChanges();
        return plan;
    }

    /// <summary>Plandan hiç borç yazılmadıysa planı siler (yanlış giriş düzeltmesi).</summary>
    public void PlanSil(int planId)
    {
        using var db = fabrika.Olustur();
        var plan = db.UcretPlanlari.Find(planId) ?? throw new IslemHatasi("Plan bulunamadı.");
        if (db.Tahakkuklar.Any(t => t.UcretPlaniId == planId))
            throw new IslemHatasi("Bu plandan borç yazılmış; silinemez. Yeni bir plan ekleyerek ücreti değiştirin.");
        db.UcretPlanlari.Remove(plan);
        db.SaveChanges();
    }

    /// <summary>Planı belirli bir aydan sonra durdurur (o ay dahil borç yazılır).</summary>
    public void PlanBitir(int planId, DateOnly? sonAy)
    {
        using var db = fabrika.Olustur();
        var plan = db.UcretPlanlari.Find(planId) ?? throw new IslemHatasi("Plan bulunamadı.");
        if (sonAy is { } s && Donem.AyBasi(s) < plan.GecerlilikBaslangic)
            throw new IslemHatasi("Bitiş ayı başlangıç ayından önce olamaz.");
        plan.GecerlilikBitis = sonAy is { } x ? Donem.AyBasi(x) : null;
        db.SaveChanges();
    }

    // ---- Cari ---------------------------------------------------------------------------

    public (List<EkstreSatiri> Ekstre, CariOzet Ozet) Cari(int mukellefId, DateOnly bugun)
    {
        using var db = fabrika.Olustur();
        var m = db.Mukellefler.AsNoTracking().First(x => x.Id == mukellefId);
        var borclar = db.Tahakkuklar.AsNoTracking().Where(t => t.MukellefId == mukellefId).ToList();
        var odemeler = db.Tahsilatlar.AsNoTracking().Where(t => t.MukellefId == mukellefId).ToList();
        return (CariHesaplayici.Ekstre(borclar, odemeler), CariHesaplayici.Ozet(borclar, odemeler, bugun, m.OdemeGunu));
    }

    /// <summary>Toplu devir girişi için aktif mükellefler, notları ve şimdiye kadar girilmiş devir toplamı.</summary>
    public List<(int Id, string Unvan, string? Notlar, decimal MevcutDevir)> DevirListesi()
    {
        using var db = fabrika.Olustur();
        var devirler = db.Tahakkuklar.AsNoTracking()
            .Where(t => t.Kaynak == TahakkukKaynagi.Devir && !t.IptalEdildi)
            .Select(t => new { t.MukellefId, t.Tutar }).ToList()
            .GroupBy(t => t.MukellefId).ToDictionary(g => g.Key, g => g.Sum(t => t.Tutar));
        return db.Mukellefler.AsNoTracking().Where(m => m.Durum != MukellefDurumu.Ayrildi).ToList()
            .OrderBy(m => m.Unvan, Turkce.Karsilastirici)
            .Select(m => (m.Id, m.Unvan, m.Notlar, devirler.GetValueOrDefault(m.Id)))
            .ToList();
    }

    /// <summary>Birden çok mükellefe aynı tarihli devir borcu girer (tek işlemde).</summary>
    public int TopluDevirGir(IEnumerable<(int MukellefId, decimal Tutar)> devirler, DateOnly tarih, int? kullaniciId)
    {
        var liste = devirler.Where(d => d.Tutar != 0).ToList();
        if (liste.Any(d => d.Tutar < 0)) throw new IslemHatasi("Devir tutarı eksi olamaz. Fazla ödemeleri tahsilat olarak girin.");
        using var db = fabrika.Olustur();
        var simdi = DateTime.Now;
        foreach (var (mukellefId, tutar) in liste)
        {
            db.Tahakkuklar.Add(new Tahakkuk
            {
                MukellefId = mukellefId,
                HizmetTuruId = HizmetTuru.DevirId,
                DonemBaslangic = tarih,
                DonemBitis = tarih,
                Tarih = tarih,
                Tutar = tutar,
                Aciklama = "Devir (açılış bakiyesi)",
                Kaynak = TahakkukKaynagi.Devir,
                OlusturanKullaniciId = kullaniciId,
                OlusturmaTarihi = simdi,
            });
        }
        db.SaveChanges();
        return liste.Count;
    }

    /// <summary>Uygulamaya geçişte mükellefin mevcut borcunu (veya alacağını) tek kalemde girer.</summary>
    public Tahakkuk DevirGir(int mukellefId, DateOnly tarih, decimal tutar, string? aciklama, int? kullaniciId)
    {
        if (tutar == 0) throw new IslemHatasi("Devir tutarı sıfır olamaz.");
        if (tutar < 0) throw new IslemHatasi("Fazla ödeme (avans) devri için tutarı ödeme olarak girin.");
        using var db = fabrika.Olustur();
        var t = new Tahakkuk
        {
            MukellefId = mukellefId,
            HizmetTuruId = HizmetTuru.DevirId,
            DonemBaslangic = tarih,
            DonemBitis = tarih,
            Tarih = tarih,
            Tutar = tutar,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Devir (açılış bakiyesi)" : aciklama.Trim(),
            Kaynak = TahakkukKaynagi.Devir,
            OlusturanKullaniciId = kullaniciId,
            OlusturmaTarihi = DateTime.Now,
        };
        db.Tahakkuklar.Add(t);
        db.SaveChanges();
        return t;
    }
}

public static class Turkce
{
    public static readonly System.Globalization.CultureInfo Kultur = new("tr-TR");
    public static readonly StringComparer Karsilastirici = StringComparer.Create(Kultur, ignoreCase: true);
}
