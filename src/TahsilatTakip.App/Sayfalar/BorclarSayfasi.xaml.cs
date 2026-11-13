using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class BorclarSayfasi
{
    public BorclarSayfasi(BorclarModeli model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }
}

public partial class BorclarModeli(TahakkukServisi servis, Oturum oturum) : ObservableObject
{
    private List<BorcSatiri> _liste = [];

    public ObservableCollection<BorcSatiri> Gorunen { get; } = [];

    [ObservableProperty] private DateTime? _baslangic;
    [ObservableProperty] private DateTime? _bitis;
    [ObservableProperty] private string _arama = "";
    [ObservableProperty] private bool _sadeceAcik = true;
    [ObservableProperty] private bool _iptalDahil;
    [ObservableProperty] private BorcSatiri? _secili;
    [ObservableProperty] private string _ozet = "";

    partial void OnBaslangicChanged(DateTime? value) => Yukle();
    partial void OnBitisChanged(DateTime? value) => Yukle();
    partial void OnSadeceAcikChanged(bool value) => Yukle();
    partial void OnIptalDahilChanged(bool value) => Yukle();
    partial void OnAramaChanged(string value) => Filtrele();

    public void Yukle()
    {
        _liste = servis.Listele(new BorcFiltresi(
            Baslangic is { } b ? DateOnly.FromDateTime(b) : null,
            Bitis is { } s ? DateOnly.FromDateTime(s) : null,
            SadeceAcik: SadeceAcik,
            IptalDahil: IptalDahil));
        Filtrele();
    }

    private void Filtrele()
    {
        var aranan = Arama.Trim();
        Gorunen.Clear();
        foreach (var b in _liste.Where(b => aranan.Length == 0
                     || Turkce.Kultur.CompareInfo.IndexOf(b.Unvan, aranan, System.Globalization.CompareOptions.IgnoreCase) >= 0))
            Gorunen.Add(b);
        var gecerli = Gorunen.Where(b => !b.IptalEdildi).ToList();
        Ozet = $"{gecerli.Count} borç · tutar {Sayi.Yaz(gecerli.Sum(b => b.Tutar))} ₺ · ödenmemiş kalan {Sayi.Yaz(gecerli.Sum(b => b.Kalan))} ₺";
    }

    [RelayCommand]
    private void Yeni()
    {
        if (BorcPenceresi.Ac()) Yukle();
    }

    [RelayCommand]
    private void Iptal()
    {
        if (Secili is null) throw new IslemHatasi("Önce listeden bir borç seçin.");
        if (Secili.IptalEdildi) throw new IslemHatasi("Bu borç zaten iptal edilmiş.");
        var neden = GirdiPenceresi.Sor("Borcu iptal et",
            $"{Secili.Unvan} - {Secili.Aciklama} ({Sayi.Yaz(Secili.Tutar)} ₺) iptal edilecek. Otomatik borçlar iptal edilse de aynı dönem için tekrar yazılmaz. İptal nedenini yazın:",
            "İptal et");
        if (neden is null) return;
        servis.IptalEt(Secili.Id, neden, oturum.Kullanici?.Id);
        Yukle();
    }
}
