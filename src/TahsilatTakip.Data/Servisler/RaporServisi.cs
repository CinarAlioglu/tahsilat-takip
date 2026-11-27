using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

/// <summary>Excel raporları ve PDF cari ekstre.</summary>
public sealed class RaporServisi(
    TahsilatServisi tahsilatlar, MukellefServisi mukellefler, TakipServisi takip, AyarServisi ayarlar)
{
    static RaporServisi()
    {
        // QuestPDF topluluk lisansı: yıllık cirosu 1 milyon USD altındaki işletmeler için ücretsiz.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string ParaBicimi = "#,##0.00";

    // ---- Excel ---------------------------------------------------------------------------

    public void TahsilatRaporu(DateOnly baslangic, DateOnly bitis, string dosya)
    {
        var liste = tahsilatlar.Listele(new TahsilatFiltresi(baslangic, bitis)).OrderBy(t => t.Tarih).ThenBy(t => t.Id).ToList();
        using var wb = new XLWorkbook();

        var ws = wb.AddWorksheet("Tahsilatlar");
        string[] baslik = ["Tarih", "Mükellef", "Tutar", "Yöntem", "Tahsil eden", "Hesap", "Ödeyen", "Açıklama", "Teslim durumu", "Kaydeden"];
        BaslikYaz(ws, baslik);
        var r = 2;
        foreach (var t in liste)
        {
            ws.Cell(r, 1).Value = t.Tarih.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = t.Unvan;
            ws.Cell(r, 3).Value = t.Tutar;
            ws.Cell(r, 4).Value = YontemAdi(t.OdemeYontemi);
            ws.Cell(r, 5).Value = t.TahsilEden;
            ws.Cell(r, 6).Value = t.Hesap;
            ws.Cell(r, 7).Value = t.OdeyenKisi;
            ws.Cell(r, 8).Value = t.Aciklama;
            ws.Cell(r, 9).Value = t.TeslimDurumu;
            ws.Cell(r, 10).Value = t.Kaydeden;
            r++;
        }
        ws.Cell(r, 2).Value = "TOPLAM";
        ws.Cell(r, 3).FormulaA1 = $"SUM(C2:C{Math.Max(2, r - 1)})";
        ws.Row(r).Style.Font.Bold = true;
        ws.Column(1).Style.DateFormat.Format = "dd.mm.yyyy";
        ws.Column(3).Style.NumberFormat.Format = ParaBicimi;
        Bitir(ws);

        var oz = wb.AddWorksheet("Özet");
        oz.Cell(1, 1).Value = $"Tahsilat özeti {baslangic:dd.MM.yyyy} - {bitis:dd.MM.yyyy}";
        oz.Cell(1, 1).Style.Font.Bold = true;
        oz.Cell(1, 1).Style.Font.FontSize = 14;
        BaslikYaz(oz, ["Tahsil eden", "Adet", "Tutar"], 3);
        r = 4;
        foreach (var g in liste.GroupBy(t => string.IsNullOrWhiteSpace(t.TahsilEden) ? "Belirtilmemiş" : t.TahsilEden!)
                     .OrderByDescending(g => g.Sum(t => t.Tutar)))
        {
            oz.Cell(r, 1).Value = g.Key;
            oz.Cell(r, 2).Value = g.Count();
            oz.Cell(r, 3).Value = g.Sum(t => t.Tutar);
            r++;
        }
        r++;
        BaslikYaz(oz, ["Ay", "Adet", "Tutar"], r);
        r++;
        foreach (var g in liste.GroupBy(t => new DateOnly(t.Tarih.Year, t.Tarih.Month, 1)).OrderBy(g => g.Key))
        {
            oz.Cell(r, 1).Value = $"{Donem.AyAdlari[g.Key.Month - 1]} {g.Key.Year}";
            oz.Cell(r, 2).Value = g.Count();
            oz.Cell(r, 3).Value = g.Sum(t => t.Tutar);
            r++;
        }
        r++;
        BaslikYaz(oz, ["Ödeme yöntemi", "Adet", "Tutar"], r);
        r++;
        foreach (var g in liste.GroupBy(t => t.OdemeYontemi))
        {
            oz.Cell(r, 1).Value = YontemAdi(g.Key);
            oz.Cell(r, 2).Value = g.Count();
            oz.Cell(r, 3).Value = g.Sum(t => t.Tutar);
            r++;
        }
        oz.Column(3).Style.NumberFormat.Format = ParaBicimi;
        oz.Columns().AdjustToContents();

        wb.SaveAs(dosya);
    }

    public void BakiyeListesi(DateOnly bugun, string dosya)
    {
        var liste = mukellefler.Listele(bugun);
        var yaslar = takip.Gecikenler(bugun, sadeceGecikmis: false).ToDictionary(g => g.Id);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Bakiyeler");
        BaslikYaz(ws, ["Mükellef", "Durum", "Ücret", "Telefon", "Bakiye", "Gecikme (gün)", "0-30 gün", "31-60 gün", "61-90 gün", "90+ gün"]);
        var r = 2;
        foreach (var m in liste)
        {
            ws.Cell(r, 1).Value = m.Unvan;
            ws.Cell(r, 2).Value = m.Durum switch { MukellefDurumu.Aktif => "Aktif", MukellefDurumu.Pasif => "Pasif", _ => "Ayrıldı" };
            ws.Cell(r, 3).Value = m.UcretBilgisi;
            ws.Cell(r, 4).Value = m.Telefon;
            ws.Cell(r, 5).Value = m.Bakiye;
            if (m.GecikmeGunu > 0) ws.Cell(r, 6).Value = m.GecikmeGunu;
            if (yaslar.TryGetValue(m.Id, out var y))
            {
                ws.Cell(r, 7).Value = y.Gun0_30;
                ws.Cell(r, 8).Value = y.Gun31_60;
                ws.Cell(r, 9).Value = y.Gun61_90;
                ws.Cell(r, 10).Value = y.Gun90Ustu;
            }
            if (m.Bakiye > 0) ws.Cell(r, 5).Style.Font.FontColor = XLColor.FromHtml("#C42B1C");
            r++;
        }
        ws.Cell(r, 1).Value = "TOPLAM";
        for (var c = 5; c <= 10; c++)
            if (c != 6) ws.Cell(r, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(Math.Max(2, r - 1), c).Address})";
        ws.Row(r).Style.Font.Bold = true;
        foreach (var c in new[] { 5, 7, 8, 9, 10 }) ws.Column(c).Style.NumberFormat.Format = ParaBicimi;
        Bitir(ws);
        wb.SaveAs(dosya);
    }

    public void AylikDurumTablosu(int yil, DateOnly bugun, string dosya)
    {
        var liste = takip.AylikDurum(yil, bugun);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet($"{yil} ödeme durumu");
        BaslikYaz(ws, ["Mükellef", "Ücret", $"{yil} öncesi kalan", .. Donem.AyAdlari, "Bakiye"]);
        var r = 2;
        foreach (var s in liste)
        {
            ws.Cell(r, 1).Value = s.Unvan;
            ws.Cell(r, 2).Value = s.UcretBilgisi;
            if (s.OncekiKalan > 0) ws.Cell(r, 3).Value = s.OncekiKalan;
            for (var i = 0; i < 12; i++)
            {
                var c = ws.Cell(r, 4 + i);
                (c.Value, var renk) = s.Aylar[i] switch
                {
                    AyDurumu.Odendi => ("✓", "#DFF6DD"),
                    AyDurumu.Kismi => ("Kısmi", "#FFF4CE"),
                    AyDurumu.Acik => ("✗", "#FDE7E9"),
                    _ => ("", (string?)null),
                };
                if (renk is not null) c.Style.Fill.BackgroundColor = XLColor.FromHtml(renk);
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            ws.Cell(r, 16).Value = s.Bakiye;
            r++;
        }
        ws.Column(3).Style.NumberFormat.Format = ParaBicimi;
        ws.Column(16).Style.NumberFormat.Format = ParaBicimi;
        Bitir(ws);
        ws.Cell(r + 1, 1).Value = "✓ ödendi · Kısmi: kısmen ödendi · ✗ ödenmedi · boş: borç yazılmamış";
        ws.Cell(r + 1, 1).Style.Font.Italic = true;
        wb.SaveAs(dosya);
    }

    private static void BaslikYaz(IXLWorksheet ws, IEnumerable<string> basliklar, int satir = 1)
    {
        var c = 1;
        foreach (var b in basliklar)
        {
            var h = ws.Cell(satir, c++);
            h.Value = b;
            h.Style.Font.Bold = true;
            h.Style.Fill.BackgroundColor = XLColor.FromHtml("#E1EDF5");
        }
    }

    private static void Bitir(IXLWorksheet ws)
    {
        ws.SheetView.FreezeRows(1);
        if (ws.LastRowUsed() is { } son && son.RowNumber() > 1) ws.Range(1, 1, son.RowNumber() - 1, ws.LastColumnUsed()!.ColumnNumber()).SetAutoFilter();
        ws.Columns().AdjustToContents(1, 200, 8, 60);
    }

    private static string YontemAdi(OdemeYontemi y) => y switch
    {
        OdemeYontemi.Nakit => "Nakit",
        OdemeYontemi.HavaleEft => "Havale/EFT",
        OdemeYontemi.KrediKarti => "Kredi kartı",
        OdemeYontemi.CekSenet => "Çek/Senet",
        _ => y.ToString(),
    };

    // ---- PDF cari ekstre --------------------------------------------------------------------

    public void EkstrePdf(int mukellefId, DateOnly bugun, string dosya)
    {
        var m = mukellefler.Getir(mukellefId) ?? throw new IslemHatasi("Mükellef bulunamadı.");
        var (ekstre, ozet) = mukellefler.Cari(mukellefId, bugun, icKullanim: false);
        var ofis = ayarlar.Oku(AyarServisi.OfisAdi) ?? "Cari Hesap Ekstresi";
        var ofisTel = ayarlar.Oku(AyarServisi.OfisTelefonu);
        var iban = ayarlar.Oku(AyarServisi.MesajIban);
        var tr = Turkce.Kultur;
        string Para(decimal d) => d.ToString("N2", tr);

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9.5f));

            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(ofis).FontSize(14).SemiBold();
                        if (ofisTel is not null) c.Item().Text(ofisTel).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("CARİ HESAP EKSTRESİ").SemiBold();
                        c.Item().AlignRight().Text($"Tarih: {bugun:dd.MM.yyyy}");
                    });
                });
                col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                col.Item().Text(m.Unvan).FontSize(12).SemiBold();
                if (m.VknTckn is not null) col.Item().Text($"VKN/TCKN: {m.VknTckn}");
                col.Item().PaddingBottom(8);
            });

            page.Content().Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);
                        c.RelativeColumn();
                        c.ConstantColumn(72);
                        c.ConstantColumn(72);
                        c.ConstantColumn(78);
                    });
                    t.Header(h =>
                    {
                        foreach (var (b, sag) in new[] { ("Tarih", false), ("Açıklama", false), ("Borç", true), ("Ödeme", true), ("Bakiye", true) })
                        {
                            var hucre = h.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                            (sag ? hucre.AlignRight() : hucre).Text(b).SemiBold();
                        }
                    });
                    foreach (var s in ekstre)
                    {
                        IContainer H(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
                        t.Cell().Element(H).Text($"{s.Tarih:dd.MM.yyyy}");
                        t.Cell().Element(H).Text(s.Aciklama);
                        t.Cell().Element(H).AlignRight().Text(s.Borc > 0 ? Para(s.Borc) : "");
                        t.Cell().Element(H).AlignRight().Text(s.Odeme > 0 ? Para(s.Odeme) : "");
                        t.Cell().Element(H).AlignRight().Text(Para(s.Bakiye));
                    }
                });

                col.Item().PaddingTop(12).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Toplam borç: {Para(ozet.ToplamBorc)} ₺");
                    c.Item().AlignRight().Text($"Toplam ödeme: {Para(ozet.ToplamOdeme)} ₺");
                    c.Item().AlignRight().Text(ozet.Bakiye >= 0
                        ? $"Kalan borç: {Para(ozet.Bakiye)} ₺"
                        : $"Fazla ödeme (alacak): {Para(-ozet.Bakiye)} ₺").FontSize(12).SemiBold();
                });
                if (iban is not null && ozet.Bakiye > 0)
                    col.Item().PaddingTop(16).Text($"Ödeme için IBAN: {iban}");
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Sayfa ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        })).GeneratePdf(dosya);
    }
}
