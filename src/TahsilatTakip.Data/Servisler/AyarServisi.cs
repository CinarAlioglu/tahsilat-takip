using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

/// <summary>Anahtar/değer ayarları (ofis adı, hatırlatma mesajındaki IBAN vb.).</summary>
public sealed class AyarServisi(VeritabaniFabrikasi fabrika)
{
    public const string OfisAdi = "OfisAdi";
    public const string OfisTelefonu = "OfisTelefonu";
    public const string MesajIban = "MesajIban";

    public string? Oku(string anahtar)
    {
        using var db = fabrika.Olustur();
        return db.Ayarlar.Find(anahtar)?.Deger;
    }

    public void Yaz(string anahtar, string? deger)
    {
        deger = string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
        using var db = fabrika.Olustur();
        var ayar = db.Ayarlar.Find(anahtar);
        if (ayar is null) db.Ayarlar.Add(new Ayar { Anahtar = anahtar, Deger = deger });
        else ayar.Deger = deger;
        db.SaveChanges();
    }
}
