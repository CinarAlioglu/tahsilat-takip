using ClosedXML.Excel;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed class AktarimSatiri
{
    public int SatirNo { get; init; }
    public string Unvan { get; init; } = "";
    public UcretPeriyodu? Periyot { get; init; }
    public decimal? Ucret { get; init; }
    public OdemeTercihi OdemeTercihi { get; init; }
    public string? Notlar { get; init; }
    /// <summary>Aynı ünvanla zaten kayıtlı; aktarılmaz.</summary>
    public bool ZatenKayitli { get; set; }
    public List<string> Uyarilar { get; } = [];
    public bool Secili { get; set; } = true;

    public string PeriyotMetni => Periyot is { } p ? Donem.PeriyotAdi(p) : "—";
    public string UyariMetni => string.Join("; ", Uyarilar);
}

public sealed record AktarimSonucu(int EklenenMukellef, int EklenenPlan, int Atlanan);

/// <summary>
/// "Mükellef Aktarım Listesi" Excel dosyasından mükellefleri ve ücretlerini içe aktarır.
/// Beklenen sütunlar (başlık satırında): Mükellef / Ünvan, Ücret Periyodu, Ücret (TL), Ödeme Tercihi, Excel notları.
/// Ödemeler aktarılmaz; geçmiş borçlar "Devir" olarak ayrıca girilir.
/// </summary>
public sealed class ExcelAktarimServisi(VeritabaniFabrikasi fabrika)
{
    public List<AktarimSatiri> Oku(string dosyaYolu)
    {
        using var akis = new FileStream(dosyaYolu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(akis);
        var ws = wb.Worksheets.First();

        // Başlık satırını ilk 10 satırda ara.
        IXLRow? baslik = null;
        foreach (var r in ws.RowsUsed().Take(10))
        {
            if (r.CellsUsed().Any(c => Normallestir(c.GetString()).Contains("UNVAN")))
            {
                baslik = r;
                break;
            }
        }
        if (baslik is null)
            throw new IslemHatasi("Başlık satırı bulunamadı. İlk sayfada \"Mükellef / Ünvan\" sütunu olmalıdır.");

        int? Sutun(params string[] anahtarlar) => baslik.CellsUsed()
            .FirstOrDefault(c => anahtarlar.Any(a => Normallestir(c.GetString()).Contains(a)))?.Address.ColumnNumber;

        var unvanSutun = Sutun("UNVAN")!.Value;
        var periyotSutun = Sutun("PERIYO");
        var ucretSutun = Sutun("UCRET (") ?? baslik.CellsUsed()
            .FirstOrDefault(c => Normallestir(c.GetString()) is var h && h.Contains("UCRET") && !h.Contains("PERIYO"))
            ?.Address.ColumnNumber;
        var tercihSutun = Sutun("TERCIH");
        var notSutun = Sutun("NOT");

        var satirlar = new List<AktarimSatiri>();
        foreach (var r in ws.RowsUsed().Where(r => r.RowNumber() > baslik.RowNumber()))
        {
            var unvan = string.Join(' ', r.Cell(unvanSutun).GetString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (unvan.Length == 0) continue;

            var periyotMetni = periyotSutun is { } ps ? Normallestir(r.Cell(ps).GetString()) : "";
            UcretPeriyodu? periyot = periyotMetni switch
            {
                var x when x.Contains('3') => UcretPeriyodu.UcAylik,
                var x when x.StartsWith("AYLIK") => UcretPeriyodu.Aylik,
                var x when x.StartsWith("YILLIK") => UcretPeriyodu.Yillik,
                _ => null,
            };

            decimal? ucret = null;
            if (ucretSutun is { } us && !r.Cell(us).IsEmpty())
            {
                var hucre = r.Cell(us);
                if (hucre.DataType == XLDataType.Number) ucret = (decimal)hucre.GetDouble();
                else if (decimal.TryParse(hucre.GetString().Replace("TL", "").Trim(),
                             System.Globalization.NumberStyles.Number, Turkce.Kultur, out var d)) ucret = d;
            }

            var tercihMetni = tercihSutun is { } ts ? Normallestir(r.Cell(ts).GetString()) : "";
            var tercih = tercihMetni.Contains("IBAN") ? OdemeTercihi.Iban
                : tercihMetni.Contains("ELDEN") || tercihMetni.Contains("NAKIT") ? OdemeTercihi.Elden
                : OdemeTercihi.Belirtilmedi;

            var not = notSutun is { } ns ? r.Cell(ns).GetString().Trim() : null;

            var satir = new AktarimSatiri
            {
                SatirNo = r.RowNumber(),
                Unvan = unvan,
                Periyot = periyot,
                Ucret = ucret,
                OdemeTercihi = tercih,
                Notlar = string.IsNullOrWhiteSpace(not) ? null : not,
            };
            if (periyot is null) satir.Uyarilar.Add("Periyot yok: ücret planı oluşturulmayacak");
            if (ucret is null or <= 0) satir.Uyarilar.Add("Ücret yok: ücret planı oluşturulmayacak");
            satirlar.Add(satir);
        }

        // Dosya içi tekrar ve veritabanında zaten kayıtlı olanlar.
        using var db = fabrika.Olustur();
        var kayitli = db.Mukellefler.Select(m => m.Unvan).ToList().ToHashSet(Turkce.Karsilastirici);
        var gorulen = new HashSet<string>(Turkce.Karsilastirici);
        foreach (var s in satirlar)
        {
            if (kayitli.Contains(s.Unvan))
            {
                s.ZatenKayitli = true;
                s.Secili = false;
                s.Uyarilar.Insert(0, "Zaten kayıtlı: atlanacak");
            }
            else if (!gorulen.Add(s.Unvan))
            {
                s.Secili = false;
                s.Uyarilar.Insert(0, "Dosyada tekrar ediyor: atlanacak");
            }
        }
        return satirlar;
    }

    /// <summary>
    /// Seçili satırları aktarır. <paramref name="gecisAyi"/>: uygulamanın borç yazmaya başlayacağı ilk ay.
    /// Aylık planlar bu aydan, 3 aylık/yıllık planlar bu ayı içeren dönemin başından başlar.
    /// </summary>
    public AktarimSonucu Aktar(IEnumerable<AktarimSatiri> satirlar, DateOnly gecisAyi)
    {
        gecisAyi = Donem.AyBasi(gecisAyi);
        using var db = fabrika.Olustur();
        var kayitli = db.Mukellefler.Select(m => m.Unvan).ToList().ToHashSet(Turkce.Karsilastirici);
        var simdi = DateTime.Now;
        int mukellefSayisi = 0, planSayisi = 0, atlanan = 0;

        foreach (var s in satirlar)
        {
            if (!s.Secili || s.ZatenKayitli || !kayitli.Add(s.Unvan))
            {
                atlanan++;
                continue;
            }
            var m = new Mukellef
            {
                Unvan = s.Unvan,
                OdemeTercihi = s.OdemeTercihi,
                Notlar = s.Notlar is null ? null : $"[Excel'den aktarıldı] {s.Notlar}",
                Durum = MukellefDurumu.Aktif,
                OlusturmaTarihi = simdi,
            };
            if (s.Periyot is { } p && s.Ucret is > 0)
            {
                m.UcretPlanlari.Add(new UcretPlani
                {
                    HizmetTuruId = HizmetTuru.AylikMuhasebeUcretiId,
                    Periyot = p,
                    Tutar = s.Ucret.Value,
                    GecerlilikBaslangic = Donem.DonemBaslangici(p, gecisAyi),
                    Aciklama = "Excel'den aktarıldı",
                    OlusturmaTarihi = simdi,
                });
                planSayisi++;
            }
            db.Mukellefler.Add(m);
            mukellefSayisi++;
        }
        db.SaveChanges();
        return new AktarimSonucu(mukellefSayisi, planSayisi, atlanan);
    }

    private static string Normallestir(string s) => s.Trim()
        .ToUpper(Turkce.Kultur)
        .Replace('İ', 'I').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ş', 'S').Replace('Ç', 'C').Replace('Ğ', 'G');
}
