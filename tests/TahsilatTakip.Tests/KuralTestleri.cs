using TahsilatTakip.Core.Guvenlik;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Tests;

public class DonemTestleri
{
    [Fact]
    public void Ekim_ucreti_1_Kasimda_borclanir()
    {
        var ekim = new DateOnly(2026, 10, 1);
        Assert.Equal(new DateOnly(2026, 10, 31), Donem.DonemBitisi(UcretPeriyodu.Aylik, ekim));
        Assert.Equal(new DateOnly(2026, 11, 1), Donem.BorcTarihi(UcretPeriyodu.Aylik, ekim));
    }

    [Fact]
    public void Ceyrek_ve_yil_donemleri_takvime_gore()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), Donem.DonemBaslangici(UcretPeriyodu.UcAylik, new DateOnly(2026, 11, 13)));
        Assert.Equal(new DateOnly(2027, 1, 1), Donem.BorcTarihi(UcretPeriyodu.UcAylik, new DateOnly(2026, 10, 1)));
        Assert.Equal(new DateOnly(2027, 1, 1), Donem.BorcTarihi(UcretPeriyodu.Yillik, new DateOnly(2026, 1, 1)));
        Assert.True(Donem.DonemBasinaDenkGelir(UcretPeriyodu.UcAylik, new DateOnly(2026, 4, 1)));
        Assert.False(Donem.DonemBasinaDenkGelir(UcretPeriyodu.UcAylik, new DateOnly(2026, 5, 1)));
        Assert.Equal("Ekim-Aralık 2026", Donem.DonemAdi(UcretPeriyodu.UcAylik, new DateOnly(2026, 10, 1)));
    }
}

public class TahakkukPlanlayiciTestleri
{
    private static readonly Dictionary<int, string> Hizmetler = new()
    {
        [HizmetTuru.AylikMuhasebeUcretiId] = "Muhasebe ücreti",
        [HizmetTuru.DefterTasdikiId] = "Defter tasdiki",
    };

    private static Mukellef M(int id = 1, MukellefDurumu d = MukellefDurumu.Aktif, DateOnly? ayrilis = null) =>
        new() { Id = id, Unvan = $"Mükellef {id}", Durum = d, AyrilisTarihi = ayrilis };

    private static UcretPlani P(int id, UcretPeriyodu periyot, decimal tutar, DateOnly baslangic, int mukellefId = 1,
        int hizmet = HizmetTuru.AylikMuhasebeUcretiId, DateOnly? bitis = null) =>
        new() { Id = id, MukellefId = mukellefId, HizmetTuruId = hizmet, Periyot = periyot, Tutar = tutar,
            GecerlilikBaslangic = baslangic, GecerlilikBitis = bitis };

    private static List<TahakkukOnerisi> Hesapla(IEnumerable<Mukellef> m, IEnumerable<UcretPlani> p, DateOnly bugun,
        HashSet<(int, int, DateOnly)>? mevcut = null) =>
        TahakkukPlanlayici.Hesapla(m, p, Hizmetler, mevcut ?? [], bugun);

    [Fact]
    public void Aylik_plan_sadece_bitmis_aylari_borclandirir()
    {
        var sonuc = Hesapla([M()], [P(1, UcretPeriyodu.Aylik, 5000, new DateOnly(2026, 9, 1))], new DateOnly(2026, 11, 13));

        Assert.Equal(2, sonuc.Count); // Eylül (1 Ekim) ve Ekim (1 Kasım); Kasım henüz bitmedi
        Assert.Equal(new DateOnly(2026, 10, 1), sonuc[0].Tarih);
        Assert.Equal(new DateOnly(2026, 11, 1), sonuc[1].Tarih);
        Assert.Equal("Ekim 2026 muhasebe ücreti", sonuc[1].Aciklama);
        Assert.All(sonuc, s => Assert.Equal(5000, s.Tutar));
    }

    [Fact]
    public void Ayin_ilk_gunu_onceki_ay_borclanir()
    {
        var sonuc = Hesapla([M()], [P(1, UcretPeriyodu.Aylik, 5000, new DateOnly(2026, 10, 1))], new DateOnly(2026, 11, 1));
        Assert.Single(sonuc);
        Assert.Equal(new DateOnly(2026, 10, 1), sonuc[0].DonemBaslangic);
    }

    [Fact]
    public void Zam_sonraki_planin_basladigi_aydan_itibaren_gecerli()
    {
        var planlar = new[]
        {
            P(1, UcretPeriyodu.Aylik, 5000, new DateOnly(2026, 8, 1)),
            P(2, UcretPeriyodu.Aylik, 6000, new DateOnly(2026, 10, 1)),
        };
        var sonuc = Hesapla([M()], planlar, new DateOnly(2026, 11, 2));

        Assert.Equal([5000m, 5000m, 6000m], sonuc.Select(s => s.Tutar));
        Assert.Equal(2, sonuc[2].UcretPlaniId);
    }

    [Fact]
    public void Uc_aylik_ve_yillik_plan_donem_sonunda_borclanir()
    {
        var planlar = new[]
        {
            P(1, UcretPeriyodu.UcAylik, 7000, new DateOnly(2026, 7, 1)),
            P(2, UcretPeriyodu.Yillik, 15000, new DateOnly(2026, 1, 1), hizmet: HizmetTuru.DefterTasdikiId),
        };
        var kasim = Hesapla([M()], planlar, new DateOnly(2026, 11, 13));
        Assert.Single(kasim); // Temmuz-Eylül çeyreği 1 Ekim'de
        Assert.Equal(new DateOnly(2026, 10, 1), kasim[0].Tarih);

        var ocak = Hesapla([M()], planlar, new DateOnly(2027, 1, 1));
        Assert.Equal(3, ocak.Count);
        Assert.Contains(ocak, o => o.HizmetTuruId == HizmetTuru.DefterTasdikiId && o.Aciklama == "2026 yılı defter tasdiki");
    }

    [Fact]
    public void Daha_once_yazilmis_donem_tekrar_yazilmaz()
    {
        var mevcut = new HashSet<(int, int, DateOnly)> { (1, HizmetTuru.AylikMuhasebeUcretiId, new DateOnly(2026, 9, 1)) };
        var sonuc = Hesapla([M()], [P(1, UcretPeriyodu.Aylik, 5000, new DateOnly(2026, 9, 1))], new DateOnly(2026, 11, 13), mevcut);
        Assert.Single(sonuc);
        Assert.Equal(new DateOnly(2026, 10, 1), sonuc[0].DonemBaslangic);
    }

    [Fact]
    public void Pasif_ve_ayrilan_mukellef_kurallari()
    {
        var plan = (int mid) => P(mid, UcretPeriyodu.Aylik, 3000, new DateOnly(2026, 8, 1), mukellefId: mid);
        var mukellefler = new[]
        {
            M(1, MukellefDurumu.Pasif),
            M(2, MukellefDurumu.Ayrildi, new DateOnly(2026, 9, 30)), // Ağustos ve Eylül borçlanır
            M(3, MukellefDurumu.Ayrildi, new DateOnly(2026, 9, 15)), // yalnız Ağustos
        };
        var sonuc = Hesapla(mukellefler, [plan(1), plan(2), plan(3)], new DateOnly(2026, 11, 13));

        Assert.DoesNotContain(sonuc, s => s.MukellefId == 1);
        Assert.Equal(2, sonuc.Count(s => s.MukellefId == 2));
        Assert.Single(sonuc, s => s.MukellefId == 3);
    }

    [Fact]
    public void Bitis_ayi_dahil_borclanir()
    {
        var sonuc = Hesapla([M()],
            [P(1, UcretPeriyodu.Aylik, 4000, new DateOnly(2026, 6, 1), bitis: new DateOnly(2026, 8, 1))],
            new DateOnly(2026, 11, 13));
        Assert.Equal(3, sonuc.Count); // Haziran, Temmuz, Ağustos
    }
}

public class CariHesaplayiciTestleri
{
    private static Tahakkuk T(int id, DateOnly tarih, decimal tutar, bool iptal = false) =>
        new() { Id = id, Tarih = tarih, DonemBaslangic = tarih.AddMonths(-1), Tutar = tutar, Aciklama = $"Borç {id}", IptalEdildi = iptal };

    private static Tahsilat O(int id, DateOnly tarih, decimal tutar, bool iptal = false) =>
        new() { Id = id, Tarih = tarih, Tutar = tutar, IptalEdildi = iptal };

    [Fact]
    public void Ekstre_yuruyen_bakiye_ve_iptal_haric()
    {
        var borclar = new[] { T(1, new(2026, 9, 1), 5000), T(2, new(2026, 10, 1), 5000), T(3, new(2026, 10, 1), 999, iptal: true) };
        var odemeler = new[] { O(1, new(2026, 10, 15), 7000), O(2, new(2026, 10, 20), 100, iptal: true) };

        var ekstre = CariHesaplayici.Ekstre(borclar, odemeler);

        Assert.Equal(3, ekstre.Count);
        Assert.Equal([5000m, 10000m, 3000m], ekstre.Select(e => e.Bakiye));
    }

    [Fact]
    public void Odemeler_en_eski_borcu_kapatir_ve_gecikme_hesaplanir()
    {
        var borclar = new[] { T(1, new(2026, 9, 1), 5000), T(2, new(2026, 10, 1), 5000), T(3, new(2026, 11, 1), 5000) };
        var odemeler = new[] { O(1, new(2026, 10, 15), 7000) };

        var kapanis = CariHesaplayici.Kapanislar(borclar, odemeler);
        Assert.True(kapanis[0].Kapandi);
        Assert.Equal(3000, kapanis[1].Kalan);
        Assert.Equal(5000, kapanis[2].Kalan);

        var ozet = CariHesaplayici.Ozet(borclar, odemeler, new DateOnly(2026, 11, 13), odemeGunu: 10);
        Assert.Equal(8000, ozet.Bakiye);
        Assert.Equal(new DateOnly(2026, 10, 10), ozet.EnEskiAcikVade);
        Assert.Equal(34, ozet.GecikmeGunu);
        Assert.Equal(2, ozet.AcikBorcSayisi);
    }

    [Fact]
    public void Fazla_odeme_avans_olur()
    {
        var ozet = CariHesaplayici.Ozet([T(1, new(2026, 10, 1), 5000)], [O(1, new(2026, 10, 5), 8000)], new DateOnly(2026, 11, 13));
        Assert.Equal(-3000, ozet.Bakiye);
        Assert.Equal(3000, ozet.Avans);
        Assert.Equal(0, ozet.GecikmeGunu);
    }

    [Fact]
    public void Vade_ay_sonunu_asmaz()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), CariHesaplayici.Vade(new DateOnly(2027, 2, 1), 31));
        Assert.Equal(new DateOnly(2026, 11, 1), CariHesaplayici.Vade(new DateOnly(2026, 11, 1), null));
    }
}

public class SifreTestleri
{
    [Fact]
    public void Hash_dogrulanir_ve_yanlis_sifre_reddedilir()
    {
        var hash = SifreHasher.Hashle("gizli123");
        Assert.DoesNotContain("gizli123", hash);
        Assert.True(SifreHasher.Dogrula("gizli123", hash));
        Assert.False(SifreHasher.Dogrula("gizli124", hash));
        Assert.False(SifreHasher.Dogrula("gizli123", "bozuk"));
        Assert.NotEqual(hash, SifreHasher.Hashle("gizli123")); // her seferinde farklı tuz
    }
}
