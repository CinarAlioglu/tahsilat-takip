namespace TahsilatTakip.Core.Varliklar;

/// <summary>Silinmek yerine iptal edilen kayıtlar için ortak alanlar.</summary>
public interface IIptalEdilebilir
{
    bool IptalEdildi { get; set; }
    DateTime? IptalTarihi { get; set; }
    int? IptalEdenKullaniciId { get; set; }
    string? IptalNedeni { get; set; }
}

public class Kullanici
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string SifreHash { get; set; } = "";
    public KullaniciRolu Rol { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime? SonGirisTarihi { get; set; }
}

public class Mukellef
{
    public int Id { get; set; }
    public string Unvan { get; set; } = "";
    public string? VknTckn { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    /// <summary>Ödemeyi fiilen yapan kişi (mükellefin kendisi değilse).</summary>
    public string? OdemeSorumlusuAd { get; set; }
    public string? OdemeSorumlusuTelefon { get; set; }
    /// <summary>Ayın kaçında ödeme bekleniyor (1-31). Boşsa borç tarihi esas alınır.</summary>
    public int? OdemeGunu { get; set; }
    public OdemeTercihi OdemeTercihi { get; set; }
    public MukellefDurumu Durum { get; set; }
    public DateOnly? BaslangicTarihi { get; set; }
    public DateOnly? AyrilisTarihi { get; set; }
    public string? Notlar { get; set; }
    public int? OdemeGrubuId { get; set; }
    public OdemeGrubu? OdemeGrubu { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public List<UcretPlani> UcretPlanlari { get; set; } = [];
    public List<Tahakkuk> Tahakkuklar { get; set; } = [];
    public List<Tahsilat> Tahsilatlar { get; set; } = [];
    public List<GorusmeNotu> GorusmeNotlari { get; set; } = [];
}

public class HizmetTuru
{
    public const int AylikMuhasebeUcretiId = 1;
    public const int DefterTasdikiId = 2;
    public const int DevirId = 3;
    public const int DigerId = 4;

    public int Id { get; set; }
    public string Ad { get; set; } = "";
    /// <summary>Sistem kayıtları silinemez/yeniden adlandırılamaz.</summary>
    public bool Sistem { get; set; }
    public bool Aktif { get; set; } = true;
}

/// <summary>
/// Mükellefin bir hizmet için anlaşılan ücreti. Zam yapıldığında yeni satır eklenir;
/// bir planın bitişi, aynı hizmetteki bir sonraki planın başlangıcından bir önceki aydır.
/// </summary>
public class UcretPlani
{
    public int Id { get; set; }
    public int MukellefId { get; set; }
    public Mukellef? Mukellef { get; set; }
    public int HizmetTuruId { get; set; }
    public HizmetTuru? HizmetTuru { get; set; }
    public UcretPeriyodu Periyot { get; set; }
    public decimal Tutar { get; set; }
    /// <summary>Planın geçerli olduğu ilk ay (her zaman ayın 1'i).</summary>
    public DateOnly GecerlilikBaslangic { get; set; }
    /// <summary>İsteğe bağlı: bu aydan sonra borç yazılmaz (ayın 1'i).</summary>
    public DateOnly? GecerlilikBitis { get; set; }
    public string? Aciklama { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

/// <summary>Borç kaydı.</summary>
public class Tahakkuk : IIptalEdilebilir
{
    public int Id { get; set; }
    public int MukellefId { get; set; }
    public Mukellef? Mukellef { get; set; }
    public int HizmetTuruId { get; set; }
    public HizmetTuru? HizmetTuru { get; set; }
    public int? UcretPlaniId { get; set; }
    public DateOnly DonemBaslangic { get; set; }
    public DateOnly DonemBitis { get; set; }
    /// <summary>Borcun yazıldığı tarih (dönem bitişinin ertesi günü).</summary>
    public DateOnly Tarih { get; set; }
    public decimal Tutar { get; set; }
    public string Aciklama { get; set; } = "";
    public TahakkukKaynagi Kaynak { get; set; }
    public int? OlusturanKullaniciId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public bool IptalEdildi { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public int? IptalEdenKullaniciId { get; set; }
    public string? IptalNedeni { get; set; }
}

/// <summary>Ödeme kaydı.</summary>
public class Tahsilat : IIptalEdilebilir
{
    public int Id { get; set; }
    public int MukellefId { get; set; }
    public Mukellef? Mukellef { get; set; }
    public DateOnly Tarih { get; set; }
    public decimal Tutar { get; set; }
    public OdemeYontemi OdemeYontemi { get; set; }
    /// <summary>Ödemeyi kim tahsil etti (Ofis, Eleman, Çınar ...).</summary>
    public string? TahsilEden { get; set; }
    /// <summary>Paranın geldiği yer (kasa, hangi banka hesabı ...).</summary>
    public string? Hesap { get; set; }
    public string? OdeyenKisi { get; set; }
    /// <summary>Veri klasörüne kopyalanan dekontun göreli yolu.</summary>
    public string? DekontDosyasi { get; set; }
    public string? Aciklama { get; set; }
    /// <summary>Ödeme grubuna gelen tek ödemenin parçalarını birbirine bağlar.</summary>
    public Guid? GrupIslemNo { get; set; }
    public int? KaydedenKullaniciId { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public bool IptalEdildi { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public int? IptalEdenKullaniciId { get; set; }
    public string? IptalNedeni { get; set; }
}

public class GorusmeNotu
{
    public int Id { get; set; }
    public int MukellefId { get; set; }
    public Mukellef? Mukellef { get; set; }
    public DateTime Tarih { get; set; }
    public GorusmeKanali Kanal { get; set; }
    public string Not { get; set; } = "";
    public DateOnly? SozTarihi { get; set; }
    public decimal? SozTutari { get; set; }
    public SozDurumu SozDurumu { get; set; }
    public int? KaydedenKullaniciId { get; set; }
}

public class OdemeGrubu
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string? Aciklama { get; set; }
    public List<Mukellef> Mukellefler { get; set; } = [];
}

public class IslemGunlugu
{
    public long Id { get; set; }
    public DateTime Tarih { get; set; }
    public int? KullaniciId { get; set; }
    public string Tablo { get; set; } = "";
    public string KayitId { get; set; } = "";
    /// <summary>Eklendi / Değiştirildi / Silindi</summary>
    public string Islem { get; set; } = "";
    /// <summary>Değişen alanlar (JSON).</summary>
    public string? Detay { get; set; }
}

public class Ayar
{
    public string Anahtar { get; set; } = "";
    public string? Deger { get; set; }
}
