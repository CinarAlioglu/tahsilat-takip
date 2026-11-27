using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class TakipSayfasi
{
    private readonly TakipModeli _model;

    public TakipSayfasi(TakipModeli model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }

    private void Geciken_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.FrameworkElement { DataContext: GecikenSatiri g })
        {
            if (MukellefKartiPenceresi.Ac(g.Id)) _model.Yukle();
        }
    }
}

public partial class TakipModeli(TakipServisi takip, GorusmeServisi gorusmeler) : ObservableObject
{
    private List<GecikenSatiri> _gecikenler = [];

    public ObservableCollection<GecikenSatiri> Gecikenler { get; } = [];
    public ObservableCollection<GorusmeSatiri> Sozler { get; } = [];

    [ObservableProperty] private int _sekme;
    [ObservableProperty] private string _arama = "";
    [ObservableProperty] private bool _tumBorclular;
    [ObservableProperty] private bool _sadeceTakiptekiSozler = true;
    [ObservableProperty] private GecikenSatiri? _seciliGeciken;
    [ObservableProperty] private GorusmeSatiri? _seciliSoz;
    [ObservableProperty] private string _gecikenBasligi = "Gecikenler";
    [ObservableProperty] private string _sozBasligi = "Ödeme sözleri";
    [ObservableProperty] private string _gecikenOzeti = "";

    partial void OnAramaChanged(string value) => GecikenFiltrele();
    partial void OnTumBorclularChanged(bool value) => Yukle();
    partial void OnSadeceTakiptekiSozlerChanged(bool value) => SozleriYukle();

    public void Yukle()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        gorusmeler.SozleriGuncelle(bugun);
        _gecikenler = takip.Gecikenler(bugun, sadeceGecikmis: !TumBorclular);
        GecikenFiltrele();
        SozleriYukle();
    }

    private void GecikenFiltrele()
    {
        var aranan = Arama.Trim();
        Gecikenler.Clear();
        foreach (var g in _gecikenler.Where(g => aranan.Length == 0
                     || Turkce.Kultur.CompareInfo.IndexOf(g.Unvan, aranan, CompareOptions.IgnoreCase) >= 0))
            Gecikenler.Add(g);
        GecikenBasligi = $"Gecikenler ({_gecikenler.Count})";
        GecikenOzeti = $"{Gecikenler.Count} mükellef · toplam {Sayi.Yaz(Gecikenler.Sum(g => g.Bakiye))} ₺ · " +
                       $"90 günü geçen {Sayi.Yaz(Gecikenler.Sum(g => g.Gun90Ustu))} ₺";
    }

    private void SozleriYukle()
    {
        var takiptekiler = gorusmeler.TakiptekiSozler();
        var liste = SadeceTakiptekiSozler ? takiptekiler : gorusmeler.Listele();
        Sozler.Clear();
        foreach (var s in liste) Sozler.Add(s);
        var geciken = takiptekiler.Count(s => s.SozDurumu == SozDurumu.Tutulmadi);
        SozBasligi = geciken > 0 ? $"Ödeme sözleri ({takiptekiler.Count}, {geciken} tutulmadı)" : $"Ödeme sözleri ({takiptekiler.Count})";
    }

    private GecikenSatiri SeciliGecikenGerekli() =>
        SeciliGeciken ?? throw new IslemHatasi("Önce listeden bir mükellef seçin.");

    [RelayCommand]
    private void GorusmeEkle()
    {
        if (GorusmePenceresi.Ac(Sekme == 0 ? SeciliGeciken?.Id : SeciliSoz?.MukellefId)) Yukle();
    }

    [RelayCommand]
    private void SeciliyeGorusme()
    {
        if (GorusmePenceresi.Ac(SeciliGecikenGerekli().Id)) Yukle();
    }

    [RelayCommand]
    private void SeciliyeTahsilat()
    {
        if (TahsilatPenceresi.Ac(SeciliGecikenGerekli().Id)) Yukle();
    }

    [RelayCommand]
    private Task MesajKopyala() => OrtakIslemler.MesajiKopyala(SeciliGecikenGerekli().Id);

    [RelayCommand]
    private void Pdf()
    {
        var g = SeciliGecikenGerekli();
        OrtakIslemler.EkstrePdf(g.Id, g.Unvan);
    }

    private GorusmeSatiri SeciliSozGerekli() =>
        SeciliSoz is { SozDurumu: not SozDurumu.Yok } s ? s : throw new IslemHatasi("Önce listeden bir ödeme sözü seçin.");

    [RelayCommand]
    private void SozTutuldu()
    {
        gorusmeler.SozDurumuDegistir(SeciliSozGerekli().Id, SozDurumu.Tutuldu);
        Yukle();
    }

    [RelayCommand]
    private void SozTutulmadi()
    {
        gorusmeler.SozDurumuDegistir(SeciliSozGerekli().Id, SozDurumu.Tutulmadi);
        Yukle();
    }

    [RelayCommand]
    private void SozaGorusme()
    {
        if (GorusmePenceresi.Ac(SeciliSoz?.MukellefId ?? throw new IslemHatasi("Önce listeden bir görüşme seçin."))) Yukle();
    }
}
