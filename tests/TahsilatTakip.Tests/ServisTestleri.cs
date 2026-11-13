using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.Tests;

/// <summary>Her test geçici bir klasörde gerçek bir SQLite veritabanıyla çalışır.</summary>
public sealed class TestOrtami : IDisposable
{
    public VeriYolu Yol { get; }
    public Oturum Oturum { get; } = new();
    public VeritabaniFabrikasi Fabrika { get; }

    public TestOrtami()
    {
        Yol = new VeriYolu(Path.Combine(Path.GetTempPath(), "tahsilat-test-" + Guid.NewGuid().ToString("N")));
        Fabrika = new VeritabaniFabrikasi(Yol.BaglantiCumlesi, Oturum);
        using var db = Fabrika.Olustur();
        VeritabaniKurulumu.Hazirla(db);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Yol.Kok, true); } catch (IOException) { }
    }
}

public class ServisTestleri
{
    [Fact]
    public void Ilk_kullanici_yonetici_olur_ve_giris_yapar()
    {
        using var o = new TestOrtami();
        var servis = new KullaniciServisi(o.Fabrika, o.Oturum);
        Assert.False(servis.KullaniciVarMi());

        var k = servis.Olustur("cinar", "Çınar Alioğlu", "sifre123", KullaniciRolu.Kullanici);
        Assert.Equal(KullaniciRolu.Yonetici, k.Rol);
        Assert.False(servis.GirisYap("cinar", "yanlis"));
        Assert.True(servis.GirisYap("CINAR", "sifre123")); // kullanıcı adı büyük/küçük harf duyarsız
        Assert.True(o.Oturum.YoneticiMi);

        servis.Olustur("eleman", "Ön Muhasebe", "sifre456", KullaniciRolu.Kullanici);
        Assert.Throws<IslemHatasi>(() => servis.Olustur("eleman", "Tekrar", "sifre456", KullaniciRolu.Kullanici));
    }

    [Fact]
    public void Yonetici_olmayan_kullanici_ekleyemez()
    {
        using var o = new TestOrtami();
        var servis = new KullaniciServisi(o.Fabrika, o.Oturum);
        servis.Olustur("cinar", "Çınar", "sifre123", KullaniciRolu.Yonetici);
        servis.GirisYap("cinar", "sifre123");
        servis.Olustur("eleman", "Eleman", "sifre456", KullaniciRolu.Kullanici);
        servis.GirisYap("eleman", "sifre456");
        Assert.Throws<IslemHatasi>(() => servis.Olustur("baska", "Başka", "sifre789", KullaniciRolu.Kullanici));
    }

    [Fact]
    public void Plan_borc_ve_cari_uctan_uca()
    {
        using var o = new TestOrtami();
        var mukellefler = new MukellefServisi(o.Fabrika);
        var tahakkuk = new TahakkukServisi(o.Fabrika);

        var m = mukellefler.Kaydet(new Mukellef { Unvan = "Örnek İnşaat Ltd. Şti.", OdemeGunu = 10 });
        Assert.Throws<IslemHatasi>(() => mukellefler.Kaydet(new Mukellef { Unvan = "örnek inşaat ltd. şti." }));

        mukellefler.PlanEkle(m.Id, HizmetTuru.AylikMuhasebeUcretiId, UcretPeriyodu.Aylik, 5000, new DateOnly(2026, 9, 1));
        Assert.Throws<IslemHatasi>(() =>
            mukellefler.PlanEkle(m.Id, HizmetTuru.DefterTasdikiId, UcretPeriyodu.Yillik, 15000, new DateOnly(2026, 3, 1)));
        mukellefler.DevirGir(m.Id, new DateOnly(2026, 8, 31), 2000, null, null);

        var bugun = new DateOnly(2026, 11, 13);
        var oneriler = tahakkuk.Onizle(bugun);
        Assert.Equal(2, oneriler.Count);
        Assert.Equal(2, tahakkuk.Olustur(oneriler, null));
        Assert.Empty(tahakkuk.Onizle(bugun));          // ikinci kez yazılmaz
        Assert.Equal(0, tahakkuk.Olustur(oneriler, null));

        // Borç yazıldıktan sonra geçmişe dönük zam engellenir; ileriye dönük zam serbest.
        Assert.Throws<IslemHatasi>(() =>
            mukellefler.PlanEkle(m.Id, HizmetTuru.AylikMuhasebeUcretiId, UcretPeriyodu.Aylik, 6000, new DateOnly(2026, 10, 1)));
        mukellefler.PlanEkle(m.Id, HizmetTuru.AylikMuhasebeUcretiId, UcretPeriyodu.Aylik, 6000, new DateOnly(2026, 11, 1));

        var (ekstre, ozet) = mukellefler.Cari(m.Id, bugun);
        Assert.Equal(3, ekstre.Count);
        Assert.Equal(12000, ozet.Bakiye);

        var liste = mukellefler.Listele(bugun);
        Assert.Equal("Aylık 6.000 ₺", Assert.Single(liste).UcretBilgisi);

        // İptal edilen borç bakiyeden düşer ama tekrar otomatik yazılmaz.
        var ekim = ekstre.Single(e => e.Aciklama.StartsWith("Ekim"));
        tahakkuk.IptalEt(ekim.KayitId, "Ücretsiz ay", null);
        Assert.Equal(7000, mukellefler.Cari(m.Id, bugun).Ozet.Bakiye);
        Assert.Empty(tahakkuk.Onizle(bugun));
    }

    [Fact]
    public void Islem_gunlugu_tutulur_ve_sifre_gizlenir()
    {
        using var o = new TestOrtami();
        var kullanicilar = new KullaniciServisi(o.Fabrika, o.Oturum);
        kullanicilar.Olustur("cinar", "Çınar", "sifre123", KullaniciRolu.Yonetici);
        kullanicilar.GirisYap("cinar", "sifre123");
        var m = new MukellefServisi(o.Fabrika).Kaydet(new Mukellef { Unvan = "Test" });

        using var db = o.Fabrika.Olustur();
        var kayitlar = db.IslemGunlukleri.AsNoTracking().ToList();
        Assert.Contains(kayitlar, g => g.Tablo == "Mukellefler" && g.KayitId == m.Id.ToString()
                                       && g.Islem == "Eklendi" && g.KullaniciId == o.Oturum.Kullanici!.Id);
        Assert.All(kayitlar, g => Assert.DoesNotContain("v1$", g.Detay ?? ""));
    }

    [Fact]
    public void Excel_aktarimi_mukellef_ve_plan_olusturur()
    {
        using var o = new TestOrtami();
        var dosya = Path.Combine(o.Yol.Kok, "liste.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Mükellefler");
            ws.Cell(1, 1).Value = "Sıra";
            ws.Cell(1, 2).Value = "Mükellef / Ünvan";
            ws.Cell(1, 3).Value = "Ücret Periyodu";
            ws.Cell(1, 4).Value = "Ücret (TL)";
            ws.Cell(1, 5).Value = "Ödeme Tercihi";
            ws.Cell(1, 6).Value = "Excel notları";
            object[][] satirlar =
            [
                [1, "AYLIK MÜKELLEF", "Aylık", 6250, "", "Not var"],
                [2, "ÜÇ AYLIK  LTD.ŞTİ.", "3 Aylık", 7000, "IBAN", ""],
                [3, "YILLIK MÜKELLEF", "Yıllık", 5000, "", ""],
                [4, "PERİYOTSUZ", "", 4000, "", ""],
                [5, "aylık mükellef", "Aylık", 1, "", ""],
            ];
            for (var i = 0; i < satirlar.Length; i++)
                for (var j = 0; j < satirlar[i].Length; j++)
                    ws.Cell(i + 2, j + 1).Value = XLCellValue.FromObject(satirlar[i][j]);
            wb.SaveAs(dosya);
        }

        var servis = new ExcelAktarimServisi(o.Fabrika);
        var okunan = servis.Oku(dosya);
        Assert.Equal(5, okunan.Count);
        Assert.Equal("ÜÇ AYLIK LTD.ŞTİ.", okunan[1].Unvan); // fazla boşluklar temizlenir
        Assert.Equal(UcretPeriyodu.UcAylik, okunan[1].Periyot);
        Assert.Equal(OdemeTercihi.Iban, okunan[1].OdemeTercihi);
        Assert.Equal(6250, okunan[0].Ucret);
        Assert.NotEmpty(okunan[3].Uyarilar);
        Assert.False(okunan[4].Secili); // dosyada tekrar (Türkçe büyük/küçük harf duyarsız)

        var sonuc = servis.Aktar(okunan, new DateOnly(2026, 11, 1));
        Assert.Equal(new AktarimSonucu(4, 3, 1), sonuc);

        using var db = o.Fabrika.Olustur();
        var planlar = db.UcretPlanlari.AsNoTracking().Include(p => p.Mukellef).ToList();
        Assert.Equal(new DateOnly(2026, 11, 1), planlar.Single(p => p.Periyot == UcretPeriyodu.Aylik).GecerlilikBaslangic);
        Assert.Equal(new DateOnly(2026, 10, 1), planlar.Single(p => p.Periyot == UcretPeriyodu.UcAylik).GecerlilikBaslangic);
        Assert.Equal(new DateOnly(2026, 1, 1), planlar.Single(p => p.Periyot == UcretPeriyodu.Yillik).GecerlilikBaslangic);

        // İkinci okumada hepsi "zaten kayıtlı" görünür.
        Assert.All(servis.Oku(dosya).Where(s => s.Unvan != "aylık mükellef"), s => Assert.True(s.ZatenKayitli));
    }

    [Fact]
    public void Yedek_alinir()
    {
        using var o = new TestOrtami();
        new MukellefServisi(o.Fabrika).Kaydet(new Mukellef { Unvan = "Yedeklenecek" });
        var yedek = new YedekServisi(o.Fabrika, o.Yol).Yedekle();
        Assert.True(File.Exists(yedek));

        using var kopya = new TahsilatDbContext(VeritabaniKurulumu.Secenekler($"Data Source={yedek}"));
        Assert.Equal("Yedeklenecek", kopya.Mukellefler.Single().Unvan);
    }
}

public class TahsilatTestleri
{
    [Fact]
    public void Tahsilat_borcu_kapatir_ve_iptal_edilebilir()
    {
        using var o = new TestOrtami();
        var mukellefler = new MukellefServisi(o.Fabrika);
        var borclar = new TahakkukServisi(o.Fabrika);
        var tahsilatlar = new TahsilatServisi(o.Fabrika, o.Yol);

        var m = mukellefler.Kaydet(new Mukellef { Unvan = "Ödeyen Mükellef" });
        borclar.ManuelEkle(m.Id, HizmetTuru.AylikMuhasebeUcretiId, new DateOnly(2026, 10, 1), 5000, "Eylül ücreti", null);
        borclar.ManuelEkle(m.Id, HizmetTuru.DefterTasdikiId, new DateOnly(2026, 11, 1), 3000, "Defter tasdiki", null);

        var t = tahsilatlar.Ekle(new TahsilatGirdisi(m.Id, new DateOnly(2026, 11, 5), 6000, OdemeYontemi.HavaleEft,
            "Eleman", "Ziraat", "Eşi", null, null), null);

        var liste = borclar.Listele(new BorcFiltresi());
        Assert.Equal(0, liste.Single(b => b.Aciklama == "Eylül ücreti").Kalan);
        Assert.Equal(2000, liste.Single(b => b.Aciklama == "Defter tasdiki").Kalan);
        Assert.Single(borclar.Listele(new BorcFiltresi(SadeceAcik: true)));

        Assert.Equal(2000, mukellefler.Cari(m.Id, new DateOnly(2026, 11, 13)).Ozet.Bakiye);
        Assert.Contains("Eleman", tahsilatlar.TahsilEdenler());
        Assert.Single(tahsilatlar.Listele(new TahsilatFiltresi(TahsilEden: "eleman")));

        Assert.Throws<IslemHatasi>(() => tahsilatlar.IptalEt(t.Id, " ", null));
        tahsilatlar.IptalEt(t.Id, "Yanlış mükellefe girildi", null);
        Assert.Equal(8000, mukellefler.Cari(m.Id, new DateOnly(2026, 11, 13)).Ozet.Bakiye);
        Assert.Empty(tahsilatlar.Listele(new TahsilatFiltresi()));
        Assert.True(Assert.Single(tahsilatlar.Listele(new TahsilatFiltresi(IptalDahil: true))).IptalEdildi);
        Assert.Throws<IslemHatasi>(() => tahsilatlar.IptalEt(t.Id, "tekrar", null));
    }

    [Fact]
    public void Dekont_veri_klasorune_kopyalanir()
    {
        using var o = new TestOrtami();
        var m = new MukellefServisi(o.Fabrika).Kaydet(new Mukellef { Unvan = "A/B Ltd. Şti." });
        var kaynak = Path.Combine(o.Yol.Kok, "dekont.pdf");
        File.WriteAllText(kaynak, "pdf");
        var servis = new TahsilatServisi(o.Fabrika, o.Yol);

        var t1 = servis.Ekle(new TahsilatGirdisi(m.Id, new DateOnly(2026, 11, 5), 100, OdemeYontemi.HavaleEft, null, null, null, null, kaynak), null);
        var t2 = servis.Ekle(new TahsilatGirdisi(m.Id, new DateOnly(2026, 11, 5), 100, OdemeYontemi.HavaleEft, null, null, null, null, kaynak), null);

        Assert.Equal(Path.Combine("2026", "11", "20261105_A_B Ltd. Şti..pdf"), t1.DekontDosyasi);
        Assert.NotEqual(t1.DekontDosyasi, t2.DekontDosyasi);
        Assert.True(File.Exists(servis.DekontTamYolu(t2.DekontDosyasi)));
    }

    [Fact]
    public void Gecersiz_tahsilat_reddedilir()
    {
        using var o = new TestOrtami();
        var m = new MukellefServisi(o.Fabrika).Kaydet(new Mukellef { Unvan = "X" });
        var servis = new TahsilatServisi(o.Fabrika, o.Yol);
        Assert.Throws<IslemHatasi>(() => servis.Ekle(new TahsilatGirdisi(m.Id, new DateOnly(2026, 11, 5), 0, OdemeYontemi.Nakit, null, null, null, null, null), null));
        Assert.Throws<IslemHatasi>(() => servis.Ekle(new TahsilatGirdisi(m.Id, DateOnly.FromDateTime(DateTime.Today).AddDays(10), 10, OdemeYontemi.Nakit, null, null, null, null, null), null));
    }
}
