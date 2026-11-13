using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class MukellefKartiPenceresi
{
    private readonly MukellefKartiModeli _model;

    private MukellefKartiPenceresi(MukellefKartiModeli model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
    }

    /// <summary>Kartı açar. Bir değişiklik yapıldıysa true döner (liste yenilensin diye).</summary>
    public static bool Ac(int? mukellefId)
    {
        var model = new MukellefKartiModeli(
            App.Servisler.GetRequiredService<MukellefServisi>(),
            App.Servisler.GetRequiredService<TahakkukServisi>(),
            App.Servisler.GetRequiredService<TahsilatServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle(mukellefId);
        var pencere = new MukellefKartiPenceresi(model) { Owner = PencereYardimcisi.Sahip() };
        pencere.ShowDialog();
        return model.DegisiklikVar;
    }
}

public sealed record PlanSatiri(
    int Id, string Hizmet, UcretPeriyodu Periyot, decimal Tutar, string Baslangic, string Bitis, string? Aciklama);

public partial class MukellefKartiModeli(
    MukellefServisi servis, TahakkukServisi borclar, TahsilatServisi tahsilatlar, Oturum oturum) : ObservableObject
{
    private Mukellef _mukellef = new();

    public bool DegisiklikVar { get; private set; }

    public List<Secenek<OdemeTercihi>> OdemeTercihleri => Secenekler.OdemeTercihleri;
    public List<Secenek<MukellefDurumu>> Durumlar => Secenekler.Durumlar;
    public List<Secenek<UcretPeriyodu>> Periyotlar => Secenekler.Periyotlar;
    public List<AySecenegi> Aylar { get; } = AySecenegi.Liste();
    public List<HizmetTuru> Hizmetler { get; private set; } = [];

    public ObservableCollection<PlanSatiri> Planlar { get; } = [];
    public ObservableCollection<EkstreSatiri> Ekstre { get; } = [];

    [ObservableProperty] private int _seciliSekme;
    [ObservableProperty] private bool _yeniKayit = true;
    [ObservableProperty] private string _pencereBasligi = "Yeni mükellef";
    [ObservableProperty] private string _baslikUnvan = "Yeni mükellef";
    [ObservableProperty] private decimal _bakiye;
    [ObservableProperty] private string _cariDurum = "";
    [ObservableProperty] private EkstreSatiri? _seciliEkstre;

    // Bilgiler
    [ObservableProperty] private string _unvan = "";
    [ObservableProperty] private string? _vknTckn;
    [ObservableProperty] private string? _telefon;
    [ObservableProperty] private string? _eposta;
    [ObservableProperty] private string? _adres;
    [ObservableProperty] private string? _odemeSorumlusuAd;
    [ObservableProperty] private string? _odemeSorumlusuTelefon;
    [ObservableProperty] private string? _odemeGunu;
    [ObservableProperty] private Secenek<OdemeTercihi> _odemeTercihi = Secenekler.OdemeTercihleri[0];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(AyrildiMi))] private Secenek<MukellefDurumu> _durum = Secenekler.Durumlar[0];
    [ObservableProperty] private DateTime? _ayrilisTarihi;
    [ObservableProperty] private string? _notlar;

    public bool AyrildiMi => Durum.Deger == MukellefDurumu.Ayrildi;

    // Yeni plan
    [ObservableProperty] private HizmetTuru? _yeniHizmet;
    [ObservableProperty] private Secenek<UcretPeriyodu> _yeniPeriyot = Secenekler.Periyotlar[0];
    [ObservableProperty] private string? _yeniTutar;
    [ObservableProperty] private AySecenegi? _yeniBaslangic;
    [ObservableProperty] private string? _yeniAciklama;
    [ObservableProperty] private PlanSatiri? _seciliPlan;
    [ObservableProperty] private AySecenegi? _bitisAyi;

    // Devir
    [ObservableProperty] private DateTime? _devirTarihi;
    [ObservableProperty] private string? _devirTutari;
    [ObservableProperty] private string? _devirAciklama;

    public void Yukle(int? id)
    {
        Hizmetler = servis.HizmetTurleri();
        OnPropertyChanged(nameof(Hizmetler));
        YeniHizmet = Hizmetler.FirstOrDefault();
        var buAy = Donem.AyBasi(DateOnly.FromDateTime(DateTime.Today));
        YeniBaslangic = Aylar.FirstOrDefault(a => a.Ay == buAy.AddMonths(1));
        // Varsayılan devir tarihi: geçen ayın son günü.
        DevirTarihi = buAy.AddDays(-1).ToDateTime(TimeOnly.MinValue);

        if (id is null) return;
        _mukellef = servis.Getir(id.Value) ?? throw new IslemHatasi("Mükellef bulunamadı.");
        YeniKayit = false;
        Unvan = _mukellef.Unvan;
        VknTckn = _mukellef.VknTckn;
        Telefon = _mukellef.Telefon;
        Eposta = _mukellef.Eposta;
        Adres = _mukellef.Adres;
        OdemeSorumlusuAd = _mukellef.OdemeSorumlusuAd;
        OdemeSorumlusuTelefon = _mukellef.OdemeSorumlusuTelefon;
        OdemeGunu = _mukellef.OdemeGunu?.ToString();
        OdemeTercihi = OdemeTercihleri.First(o => o.Deger == _mukellef.OdemeTercihi);
        Durum = Durumlar.First(d => d.Deger == _mukellef.Durum);
        AyrilisTarihi = _mukellef.AyrilisTarihi?.ToDateTime(TimeOnly.MinValue);
        Notlar = _mukellef.Notlar;
        BasliklariGuncelle();
        PlanlariYukle();
        CariYukle();
    }

    private void BasliklariGuncelle()
    {
        BaslikUnvan = _mukellef.Unvan;
        PencereBasligi = "Mükellef kartı - " + _mukellef.Unvan;
    }

    private void PlanlariYukle()
    {
        Planlar.Clear();
        var planlar = servis.Planlar(_mukellef.Id);
        foreach (var p in planlar)
        {
            var sonraki = planlar.Where(x => x.HizmetTuruId == p.HizmetTuruId && x.GecerlilikBaslangic > p.GecerlilikBaslangic)
                .OrderBy(x => x.GecerlilikBaslangic).FirstOrDefault();
            var bitis = p.GecerlilikBitis is { } b ? new AySecenegi(b).Ad
                : sonraki is not null ? new AySecenegi(sonraki.GecerlilikBaslangic.AddMonths(-1)).Ad + " (zam)"
                : "Devam ediyor";
            Planlar.Add(new PlanSatiri(p.Id, p.HizmetTuru?.Ad ?? "", p.Periyot, p.Tutar,
                new AySecenegi(p.GecerlilikBaslangic).Ad, bitis, p.Aciklama));
        }
    }

    private void CariYukle()
    {
        var (ekstre, ozet) = servis.Cari(_mukellef.Id, DateOnly.FromDateTime(DateTime.Today));
        Ekstre.Clear();
        foreach (var s in ekstre.AsEnumerable().Reverse()) Ekstre.Add(s); // en yeni üstte
        Bakiye = ozet.Bakiye;
        CariDurum = ozet.Bakiye switch
        {
            > 0 when ozet.GecikmeGunu > 0 => $"{ozet.AcikBorcSayisi} açık borç · {ozet.GecikmeGunu} gün gecikmede",
            > 0 => $"{ozet.AcikBorcSayisi} açık borç · vadesi gelmedi",
            < 0 => "Fazla ödeme (avans)",
            _ => "Borcu yok",
        };
    }

    [RelayCommand]
    private void Kaydet()
    {
        int? gun = null;
        if (!string.IsNullOrWhiteSpace(OdemeGunu))
        {
            if (!int.TryParse(OdemeGunu, out var g)) throw new IslemHatasi("Ödeme günü sayı olmalıdır.");
            gun = g;
        }
        var m = new Mukellef
        {
            Id = _mukellef.Id,
            Unvan = Unvan,
            VknTckn = Bosalt(VknTckn),
            Telefon = Bosalt(Telefon),
            Eposta = Bosalt(Eposta),
            Adres = Bosalt(Adres),
            OdemeSorumlusuAd = Bosalt(OdemeSorumlusuAd),
            OdemeSorumlusuTelefon = Bosalt(OdemeSorumlusuTelefon),
            OdemeGunu = gun,
            OdemeTercihi = OdemeTercihi.Deger,
            Durum = Durum.Deger,
            AyrilisTarihi = AyrilisTarihi is { } a ? DateOnly.FromDateTime(a) : null,
            BaslangicTarihi = _mukellef.BaslangicTarihi,
            OdemeGrubuId = _mukellef.OdemeGrubuId,
            Notlar = Bosalt(Notlar),
            OlusturmaTarihi = _mukellef.OlusturmaTarihi,
        };
        _mukellef = servis.Kaydet(m);
        DegisiklikVar = true;
        BasliklariGuncelle();
        if (YeniKayit)
        {
            YeniKayit = false;
            CariYukle();
            SeciliSekme = 1; // yeni mükellefte doğrudan ücret sekmesine geç
        }
        else
        {
            CariYukle();
        }
    }

    [RelayCommand]
    private void PlanEkle()
    {
        if (YeniHizmet is null) throw new IslemHatasi("Hizmet seçin.");
        if (YeniBaslangic is null) throw new IslemHatasi("Başlangıç ayı seçin.");
        var tutar = Sayi.Oku(YeniTutar) ?? throw new IslemHatasi("Tutarı sayı olarak girin (ör. 6.250).");
        servis.PlanEkle(_mukellef.Id, YeniHizmet.Id, YeniPeriyot.Deger, tutar, YeniBaslangic.Ay, YeniAciklama);
        YeniTutar = null;
        YeniAciklama = null;
        DegisiklikVar = true;
        PlanlariYukle();
    }

    [RelayCommand]
    private async Task PlanSil()
    {
        if (SeciliPlan is null) throw new IslemHatasi("Önce listeden bir plan seçin.");
        if (!await Mesaj.Onayla("Planı sil", $"{SeciliPlan.Hizmet} - {SeciliPlan.Baslangic} planı silinsin mi?", "Sil")) return;
        servis.PlanSil(SeciliPlan.Id);
        DegisiklikVar = true;
        PlanlariYukle();
    }

    [RelayCommand]
    private void PlanBitir()
    {
        if (SeciliPlan is null) throw new IslemHatasi("Önce listeden bir plan seçin.");
        if (BitisAyi is null) throw new IslemHatasi("Son ayı seçin.");
        servis.PlanBitir(SeciliPlan.Id, BitisAyi.Ay);
        DegisiklikVar = true;
        PlanlariYukle();
    }

    [RelayCommand]
    private async Task DevirEkle()
    {
        if (DevirTarihi is null) throw new IslemHatasi("Devir tarihini seçin.");
        var tutar = Sayi.Oku(DevirTutari) ?? throw new IslemHatasi("Tutarı sayı olarak girin.");
        if (!await Mesaj.Onayla("Devir ekle",
                $"{DevirTarihi:dd.MM.yyyy} tarihli {Sayi.Yaz(tutar)} ₺ devir borcu eklensin mi?", "Ekle")) return;
        servis.DevirGir(_mukellef.Id, DateOnly.FromDateTime(DevirTarihi.Value), tutar, DevirAciklama, oturum.Kullanici?.Id);
        DevirTutari = null;
        DevirAciklama = null;
        DegisiklikVar = true;
        CariYukle();
    }

    [RelayCommand]
    private void TahsilatAl()
    {
        if (!TahsilatPenceresi.Ac(_mukellef.Id)) return;
        DegisiklikVar = true;
        CariYukle();
        SeciliSekme = 2;
    }

    [RelayCommand]
    private void ElleBorc()
    {
        if (!BorcPenceresi.Ac(_mukellef.Id)) return;
        DegisiklikVar = true;
        CariYukle();
        SeciliSekme = 2;
    }

    [RelayCommand]
    private void EkstreIptal()
    {
        if (SeciliEkstre is not { } s) throw new IslemHatasi("Önce ekstreden bir satır seçin.");
        var tur = s.Tur == EkstreSatirTuru.Borc ? "borç" : "ödeme";
        var neden = GirdiPenceresi.Sor($"Kaydı iptal et",
            $"{s.Tarih:dd.MM.yyyy} tarihli {tur} kaydı ({s.Aciklama}, {Sayi.Yaz(s.Borc + s.Odeme)} ₺) iptal edilecek. İptal nedenini yazın:",
            "İptal et");
        if (neden is null) return;
        if (s.Tur == EkstreSatirTuru.Borc) borclar.IptalEt(s.KayitId, neden, oturum.Kullanici?.Id);
        else tahsilatlar.IptalEt(s.KayitId, neden, oturum.Kullanici?.Id);
        DegisiklikVar = true;
        CariYukle();
    }

    private static string? Bosalt(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
