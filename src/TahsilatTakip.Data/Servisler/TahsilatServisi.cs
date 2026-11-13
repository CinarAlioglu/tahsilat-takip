using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed record TahsilatGirdisi(
    int MukellefId,
    DateOnly Tarih,
    decimal Tutar,
    OdemeYontemi OdemeYontemi,
    string? TahsilEden,
    string? Hesap,
    string? OdeyenKisi,
    string? Aciklama,
    string? DekontKaynakDosyasi);

public sealed record TahsilatSatiri(
    int Id,
    DateOnly Tarih,
    int MukellefId,
    string Unvan,
    decimal Tutar,
    OdemeYontemi OdemeYontemi,
    string? TahsilEden,
    string? Hesap,
    string? OdeyenKisi,
    string? Aciklama,
    string? DekontDosyasi,
    bool IptalEdildi,
    string? IptalNedeni,
    string? Kaydeden);

public sealed record TahsilatFiltresi(
    DateOnly? Baslangic = null,
    DateOnly? Bitis = null,
    int? MukellefId = null,
    string? TahsilEden = null,
    bool IptalDahil = false);

/// <summary>Ödeme kayıtları. Kayıtlar silinmez, gerekçesiyle iptal edilir.</summary>
public sealed class TahsilatServisi(VeritabaniFabrikasi fabrika, VeriYolu yol)
{
    public Tahsilat Ekle(TahsilatGirdisi g, int? kullaniciId)
    {
        if (g.Tutar <= 0) throw new IslemHatasi("Tutar sıfırdan büyük olmalıdır.");
        if (g.Tarih > DateOnly.FromDateTime(DateTime.Today).AddDays(1))
            throw new IslemHatasi("Ödeme tarihi ileri bir tarih olamaz.");

        using var db = fabrika.Olustur();
        var mukellef = db.Mukellefler.Find(g.MukellefId) ?? throw new IslemHatasi("Mükellef bulunamadı.");

        var t = new Tahsilat
        {
            MukellefId = mukellef.Id,
            Tarih = g.Tarih,
            Tutar = g.Tutar,
            OdemeYontemi = g.OdemeYontemi,
            TahsilEden = Bosalt(g.TahsilEden),
            Hesap = Bosalt(g.Hesap),
            OdeyenKisi = Bosalt(g.OdeyenKisi),
            Aciklama = Bosalt(g.Aciklama),
            KaydedenKullaniciId = kullaniciId,
            OlusturmaTarihi = DateTime.Now,
        };
        if (!string.IsNullOrWhiteSpace(g.DekontKaynakDosyasi))
            t.DekontDosyasi = DekontKopyala(g.DekontKaynakDosyasi, mukellef.Unvan, g.Tarih);

        db.Tahsilatlar.Add(t);
        db.SaveChanges();
        return t;
    }

    /// <summary>Dekontu veri klasörüne (Dekontlar\yyyy\MM) kopyalar, göreli yolu döner.</summary>
    private string DekontKopyala(string kaynak, string unvan, DateOnly tarih)
    {
        if (!File.Exists(kaynak)) throw new IslemHatasi("Dekont dosyası bulunamadı.");
        var gecersiz = Path.GetInvalidFileNameChars();
        var temizUnvan = new string(unvan.Select(c => gecersiz.Contains(c) ? '_' : c).ToArray());
        if (temizUnvan.Length > 60) temizUnvan = temizUnvan[..60];
        var goreliKlasor = Path.Combine(tarih.Year.ToString("0000"), tarih.Month.ToString("00"));
        var hedefKlasor = Path.Combine(yol.DekontKlasoru, goreliKlasor);
        Directory.CreateDirectory(hedefKlasor);

        var temelAd = $"{tarih:yyyyMMdd}_{temizUnvan.Trim()}";
        var uzanti = Path.GetExtension(kaynak);
        var ad = temelAd + uzanti;
        for (var i = 2; File.Exists(Path.Combine(hedefKlasor, ad)); i++) ad = $"{temelAd}_{i}{uzanti}";
        File.Copy(kaynak, Path.Combine(hedefKlasor, ad));
        return Path.Combine(goreliKlasor, ad);
    }

    public string? DekontTamYolu(string? goreliYol) =>
        string.IsNullOrWhiteSpace(goreliYol) ? null : Path.Combine(yol.DekontKlasoru, goreliYol);

    public List<TahsilatSatiri> Listele(TahsilatFiltresi f)
    {
        using var db = fabrika.Olustur();
        var q = db.Tahsilatlar.AsNoTracking().Include(t => t.Mukellef).AsQueryable();
        if (f.Baslangic is { } b) q = q.Where(t => t.Tarih >= b);
        if (f.Bitis is { } s) q = q.Where(t => t.Tarih <= s);
        if (f.MukellefId is { } m) q = q.Where(t => t.MukellefId == m);
        if (!f.IptalDahil) q = q.Where(t => !t.IptalEdildi);
        var liste = q.ToList();
        if (!string.IsNullOrWhiteSpace(f.TahsilEden))
            liste = liste.Where(t => Turkce.Karsilastirici.Equals(t.TahsilEden ?? "", f.TahsilEden.Trim())).ToList();

        var kullanicilar = db.Kullanicilar.AsNoTracking().ToDictionary(k => k.Id, k => k.AdSoyad);
        return liste
            .OrderByDescending(t => t.Tarih).ThenByDescending(t => t.Id)
            .Select(t => new TahsilatSatiri(t.Id, t.Tarih, t.MukellefId, t.Mukellef!.Unvan, t.Tutar, t.OdemeYontemi,
                t.TahsilEden, t.Hesap, t.OdeyenKisi, t.Aciklama, t.DekontDosyasi, t.IptalEdildi, t.IptalNedeni,
                t.KaydedenKullaniciId is { } k ? kullanicilar.GetValueOrDefault(k) : null))
            .ToList();
    }

    /// <summary>Daha önce girilmiş "tahsil eden" değerleri; girişte öneri olarak gösterilir.</summary>
    public List<string> TahsilEdenler()
    {
        using var db = fabrika.Olustur();
        var gecmis = db.Tahsilatlar.AsNoTracking().Where(t => t.TahsilEden != null).Select(t => t.TahsilEden!).ToList();
        return new[] { "Ofis", "Eleman" }.Concat(gecmis)
            .Distinct(Turkce.Karsilastirici)
            .OrderBy(x => x, Turkce.Karsilastirici)
            .ToList();
    }

    public List<string> Hesaplar()
    {
        using var db = fabrika.Olustur();
        return db.Tahsilatlar.AsNoTracking().Where(t => t.Hesap != null).Select(t => t.Hesap!).ToList()
            .Distinct(Turkce.Karsilastirici).OrderBy(x => x, Turkce.Karsilastirici).ToList();
    }

    public void IptalEt(int tahsilatId, string neden, int? kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(neden)) throw new IslemHatasi("İptal nedeni yazılmalıdır.");
        using var db = fabrika.Olustur();
        var t = db.Tahsilatlar.Find(tahsilatId) ?? throw new IslemHatasi("Kayıt bulunamadı.");
        if (t.IptalEdildi) throw new IslemHatasi("Kayıt zaten iptal edilmiş.");
        t.IptalEdildi = true;
        t.IptalTarihi = DateTime.Now;
        t.IptalEdenKullaniciId = kullaniciId;
        t.IptalNedeni = neden.Trim();
        db.SaveChanges();
    }

    private static string? Bosalt(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
