using ClosedXML.Excel;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.Tests;

public class TakipTestleri
{
    private static (TestOrtami o, MukellefServisi m, TahakkukServisi b, TahsilatServisi t) Kur()
    {
        var o = new TestOrtami();
        return (o, new MukellefServisi(o.Fabrika), new TahakkukServisi(o.Fabrika), new TahsilatServisi(o.Fabrika, o.Yol));
    }

    private static TahsilatGirdisi Odeme(int mid, DateOnly tarih, decimal tutar, string? tahsilEden = null) =>
        new(mid, tarih, tutar, OdemeYontemi.Nakit, tahsilEden, null, null, null, null);

    [Fact]
    public void Eleman_tahsilati_teslim_bekler()
    {
        var (o, m, _, t) = Kur();
        using var _o = o;
        var x = m.Kaydet(new Mukellef { Unvan = "X" });
        var ofis = t.Ekle(Odeme(x.Id, new(2026, 11, 2), 100, "Ofis"), null);
        var eleman = t.Ekle(Odeme(x.Id, new(2026, 11, 3), 200, "Eleman"), null);
        var bos = t.Ekle(Odeme(x.Id, new(2026, 11, 4), 300), null);
        Assert.True(ofis.TeslimEdildi);
        Assert.True(bos.TeslimEdildi);
        Assert.False(eleman.TeslimEdildi);

        var bekleyen = Assert.Single(t.Listele(new TahsilatFiltresi(SadeceTeslimBekleyen: true)));
        Assert.Equal(eleman.Id, bekleyen.Id);
        Assert.Equal(1, t.TeslimAl([eleman.Id, ofis.Id], new(2026, 11, 10), "Çınar"));
        Assert.Empty(t.Listele(new TahsilatFiltresi(SadeceTeslimBekleyen: true)));
        Assert.Equal("Teslim alındı 10.11.2026", t.Listele(new TahsilatFiltresi()).Single(s => s.Id == eleman.Id).TeslimDurumu);
    }

    [Fact]
    public void Soz_tutulur_veya_tutulmaz()
    {
        var (o, m, b, t) = Kur();
        using var _o = o;
        var g = new GorusmeServisi(o.Fabrika);
        var a = m.Kaydet(new Mukellef { Unvan = "A" });
        var c = m.Kaydet(new Mukellef { Unvan = "C" });
        b.ManuelEkle(a.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 10, 1), 5000, "Eylül", null);
        b.ManuelEkle(c.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 10, 1), 5000, "Eylül", null);

        var sa = g.Ekle(a.Id, new DateTime(2026, 11, 1, 10, 0, 0), GorusmeKanali.Telefon, "11'inde 5.000 ödeyecek", new(2026, 11, 11), 5000, null);
        var sc = g.Ekle(c.Id, new DateTime(2026, 11, 1, 10, 0, 0), GorusmeKanali.WhatsApp, "Hafta sonu gönderecek", new(2026, 11, 8), null, null);
        Assert.Throws<IslemHatasi>(() => g.Ekle(a.Id, DateTime.Now, GorusmeKanali.Telefon, " ", null, null, null));

        t.Ekle(Odeme(a.Id, new(2026, 11, 10), 3000), null);   // eksik ödeme
        t.Ekle(Odeme(c.Id, new(2026, 10, 30), 1000), null);   // görüşmeden önce: sayılmaz

        g.SozleriGuncelle(new(2026, 11, 9));
        var liste = g.Listele(sadeceSozler: true);
        Assert.Equal(SozDurumu.Bekliyor, liste.Single(x => x.Id == sa.Id).SozDurumu);
        Assert.Equal(SozDurumu.Tutulmadi, liste.Single(x => x.Id == sc.Id).SozDurumu);

        t.Ekle(Odeme(a.Id, new(2026, 11, 11), 2000), null);
        g.SozleriGuncelle(new(2026, 11, 12));
        Assert.Equal(SozDurumu.Tutuldu, g.Listele(a.Id).Single().SozDurumu);

        // Tutulmayan söz, mükellefle yeniden görüşülünce takipten düşer.
        Assert.Single(g.TakiptekiSozler());
        g.Ekle(c.Id, new DateTime(2026, 11, 12, 9, 0, 0), GorusmeKanali.Telefon, "Ulaşılamadı", null, null, null);
        Assert.Empty(g.TakiptekiSozler());
    }

    [Fact]
    public void Gecikenler_yas_dilimleri_ve_mesaj()
    {
        var (o, m, b, t) = Kur();
        using var _o = o;
        var ayar = new AyarServisi(o.Fabrika);
        ayar.Yaz(AyarServisi.OfisAdi, "Alioğlu Mali Müşavirlik");
        var takip = new TakipServisi(o.Fabrika, ayar);
        var x = m.Kaydet(new Mukellef { Unvan = "X Ltd.", OdemeSorumlusuAd = "Ali Bey" });
        b.ManuelEkle(x.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 8, 1), 1000, "Temmuz 2026 muhasebe ücreti", null);
        b.ManuelEkle(x.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 10, 1), 1000, "Eylül 2026 muhasebe ücreti", null);
        b.ManuelEkle(x.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 11, 1), 1000, "Ekim 2026 muhasebe ücreti", null);
        t.Ekle(Odeme(x.Id, new(2026, 8, 20), 400), null);

        var satir = Assert.Single(takip.Gecikenler(new(2026, 11, 13)));
        Assert.Equal(2600, satir.Bakiye);
        Assert.Equal(1000, satir.Gun0_30);   // 1 Kasım
        Assert.Equal(1000, satir.Gun31_60);  // 1 Ekim (43 gün)
        Assert.Equal(0, satir.Gun61_90);
        Assert.Equal(600, satir.Gun90Ustu);  // 1 Ağustos (104 gün), 400'ü ödenmiş

        var mesaj = takip.HatirlatmaMesaji(x.Id, new(2026, 11, 13));
        Assert.StartsWith("Sayın Ali Bey,", mesaj);
        Assert.Contains("2.600,00 TL", mesaj);
        Assert.Contains("- Temmuz 2026 muhasebe ücreti: 600,00 TL", mesaj);
        Assert.EndsWith("Alioğlu Mali Müşavirlik", mesaj);
    }

    [Fact]
    public void Aylik_durum_tablosu()
    {
        var (o, m, b, t) = Kur();
        using var _o = o;
        var takip = new TakipServisi(o.Fabrika, new AyarServisi(o.Fabrika));
        var x = m.Kaydet(new Mukellef { Unvan = "X" });
        m.PlanEkle(x.Id, HizmetTuru.AylikMuhasebeUcretiId, UcretPeriyodu.Aylik, 1000, new(2026, 8, 1));
        var y = m.Kaydet(new Mukellef { Unvan = "Y" });
        m.PlanEkle(y.Id, HizmetTuru.AylikMuhasebeUcretiId, UcretPeriyodu.UcAylik, 3000, new(2026, 7, 1));
        m.DevirGir(x.Id, new(2026, 7, 31), 500, null, null);
        b.Olustur(b.Onizle(new(2026, 11, 13)), null);   // X: Ağu, Eyl, Eki · Y: Tem-Eyl
        t.Ekle(Odeme(x.Id, new(2026, 9, 1), 2000), null);  // devir 500 + Ağustos 1000 + Eylül'ün 500'ü

        var tablo = takip.AylikDurum(2026, new(2026, 11, 13));
        var sx = tablo.Single(s => s.Unvan == "X");
        Assert.Equal(AyDurumu.Yok, sx.A7);
        Assert.Equal(AyDurumu.Odendi, sx.A8);
        Assert.Equal(AyDurumu.Kismi, sx.A9);
        Assert.Equal(AyDurumu.Acik, sx.A10);
        Assert.Equal(AyDurumu.Yok, sx.A11);
        Assert.Equal(1500, sx.Bakiye);
        var sy = tablo.Single(s => s.Unvan == "Y");
        Assert.Equal([AyDurumu.Acik, AyDurumu.Acik, AyDurumu.Acik], new[] { sy.A7, sy.A8, sy.A9 });
    }

    [Fact]
    public void Grup_odemesi_dagitilir_ve_birlikte_iptal_edilir()
    {
        var (o, m, b, t) = Kur();
        using var _o = o;
        var grup = new GrupServisi(o.Fabrika, t);
        var sirket = m.Kaydet(new Mukellef { Unvan = "A Şirketi" });
        var sahis = m.Kaydet(new Mukellef { Unvan = "B Şahıs" });
        var g = grup.Kaydet(null, "MSH Grubu", null);
        grup.UyeEkle(g.Id, sirket.Id);
        grup.UyeEkle(g.Id, sahis.Id);
        var g2 = grup.Kaydet(null, "Başka grup", null);
        Assert.Throws<IslemHatasi>(() => grup.UyeEkle(g2.Id, sahis.Id));

        b.ManuelEkle(sirket.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 9, 1), 4000, "Ağustos", null);
        b.ManuelEkle(sahis.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 8, 1), 3000, "Temmuz", null);
        b.ManuelEkle(sahis.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 10, 1), 3000, "Eylül", null);

        Assert.Equal(10000, grup.Listele(new(2026, 11, 13)).Single(x => x.Id == g.Id).ToplamBakiye);

        // 8.000: önce B Temmuz (3.000), sonra A Ağustos (4.000), kalan 1.000 B Eylül'e.
        var dagilim = grup.Dagit(g.Id, 8000);
        Assert.Equal(4000, dagilim.Single(d => d.MukellefId == sirket.Id).Tutar);
        Assert.Equal(4000, dagilim.Single(d => d.MukellefId == sahis.Id).Tutar);

        // Fazla ödeme ilk üyeye (alfabetik) avans yazılır.
        Assert.Equal(4000 + 2000, grup.Dagit(g.Id, 12000).Single(d => d.MukellefId == sirket.Id).Tutar);

        var kayitlar = grup.TahsilatAl(g.Id, Odeme(0, new(2026, 11, 5), 8000), null);
        Assert.Equal(2, kayitlar.Count);
        Assert.Equal(2000, grup.Listele(new(2026, 11, 13)).Single(x => x.Id == g.Id).ToplamBakiye);

        t.IptalEt(kayitlar[0].Id, "Yanlış giriş", null);
        Assert.Empty(t.Listele(new TahsilatFiltresi()));
        Assert.Equal(10000, grup.Listele(new(2026, 11, 13)).Single(x => x.Id == g.Id).ToplamBakiye);

        grup.Sil(g.Id);
        Assert.Null(m.Getir(sahis.Id)!.OdemeGrubuId);
    }

    [Fact]
    public void Raporlar_olusur()
    {
        var (o, m, b, t) = Kur();
        using var _o = o;
        var ayar = new AyarServisi(o.Fabrika);
        ayar.Yaz(AyarServisi.OfisAdi, "Alioğlu Mali Müşavirlik");
        var takip = new TakipServisi(o.Fabrika, ayar);
        var rapor = new RaporServisi(t, m, takip, ayar);
        var x = m.Kaydet(new Mukellef { Unvan = "Örnek Şirketi Ltd. Şti." });
        b.ManuelEkle(x.Id, HizmetTuru.AylikMuhasebeUcretiId, new(2026, 10, 1), 5000, "Eylül 2026 muhasebe ücreti", null);
        t.Ekle(Odeme(x.Id, new(2026, 11, 3), 2000, "Eleman"), null);

        var xlsx1 = Path.Combine(o.Yol.Kok, "tahsilat.xlsx");
        rapor.TahsilatRaporu(new(2026, 11, 1), new(2026, 11, 30), xlsx1);
        using (var wb = new XLWorkbook(xlsx1))
        {
            Assert.Equal(2000, wb.Worksheet("Tahsilatlar").Cell(2, 3).GetDouble());
            Assert.Equal("Eleman", wb.Worksheet("Özet").Cell(4, 1).GetString());
        }

        var xlsx2 = Path.Combine(o.Yol.Kok, "bakiye.xlsx");
        rapor.BakiyeListesi(new(2026, 11, 13), xlsx2);
        using (var wb = new XLWorkbook(xlsx2)) Assert.Equal(3000, wb.Worksheet(1).Cell(2, 5).GetDouble());

        var xlsx3 = Path.Combine(o.Yol.Kok, "durum.xlsx");
        rapor.AylikDurumTablosu(2026, new(2026, 11, 13), xlsx3);
        using (var wb = new XLWorkbook(xlsx3)) Assert.Equal("Kısmi", wb.Worksheet(1).Cell(2, 3 + 10).GetString());

        var pdf = Path.Combine(o.Yol.Kok, "ekstre.pdf");
        rapor.EkstrePdf(x.Id, new(2026, 11, 13), pdf);
        Assert.True(new FileInfo(pdf).Length > 1000);
    }
}
