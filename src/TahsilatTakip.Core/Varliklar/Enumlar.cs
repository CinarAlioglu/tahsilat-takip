namespace TahsilatTakip.Core.Varliklar;

public enum KullaniciRolu
{
    Kullanici = 0,
    Yonetici = 1,
}

public enum MukellefDurumu
{
    Aktif = 0,
    Pasif = 1,
    Ayrildi = 2,
}

public enum OdemeTercihi
{
    Belirtilmedi = 0,
    Elden = 1,
    Iban = 2,
}

/// <summary>Ücretin hangi aralıkla borç yazılacağı.</summary>
public enum UcretPeriyodu
{
    Aylik = 1,
    UcAylik = 3,
    Yillik = 12,
}

public enum TahakkukKaynagi
{
    /// <summary>Ücret planından dönem sonunda otomatik oluşturuldu.</summary>
    Otomatik = 0,
    /// <summary>Kullanıcı elle girdi (ek hizmet vb.).</summary>
    Manuel = 1,
    /// <summary>Uygulamaya geçişte girilen açılış bakiyesi.</summary>
    Devir = 2,
}

public enum OdemeYontemi
{
    Nakit = 0,
    HavaleEft = 1,
    KrediKarti = 2,
    CekSenet = 3,
}

public enum GorusmeKanali
{
    Telefon = 0,
    WhatsApp = 1,
    YuzYuze = 2,
    Diger = 3,
}

public enum SozDurumu
{
    /// <summary>Görüşmede ödeme sözü verilmedi.</summary>
    Yok = 0,
    Bekliyor = 1,
    Tutuldu = 2,
    Tutulmadi = 3,
}
