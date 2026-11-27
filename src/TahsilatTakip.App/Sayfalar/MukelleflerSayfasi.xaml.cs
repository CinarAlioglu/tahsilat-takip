using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class MukelleflerSayfasi
{
    private readonly MukelleflerModeli _model;

    public MukelleflerSayfasi(MukelleflerModeli model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }

    private void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: MukellefOzeti m }) _model.Ac(m.Id);
    }
}

public partial class MukelleflerModeli(MukellefServisi mukellefler) : ObservableObject
{
    private const string Tumu = "Tümü";
    private List<MukellefOzeti> _tumu = [];

    public List<string> DurumFiltreleri { get; } = ["Aktif", "Pasif", "Ayrıldı", Tumu];
    public ObservableCollection<MukellefOzeti> Gorunen { get; } = [];

    [ObservableProperty] private string _arama = "";
    [ObservableProperty] private string _durumFiltresi = "Aktif";
    [ObservableProperty] private bool _sadeceBorclular;
    [ObservableProperty] private MukellefOzeti? _secili;
    [ObservableProperty] private string _ozet = "";

    partial void OnAramaChanged(string value) => Filtrele();
    partial void OnDurumFiltresiChanged(string value) => Filtrele();
    partial void OnSadeceBorclularChanged(bool value) => Filtrele();

    public void Yukle()
    {
        _tumu = mukellefler.Listele(DateOnly.FromDateTime(DateTime.Today));
        Filtrele();
    }

    private void Filtrele()
    {
        var aranan = Arama.Trim();
        var durum = DurumFiltresi switch
        {
            "Aktif" => MukellefDurumu.Aktif,
            "Pasif" => MukellefDurumu.Pasif,
            "Ayrıldı" => MukellefDurumu.Ayrildi,
            _ => (MukellefDurumu?)null,
        };
        var sonuc = _tumu.Where(m =>
            (durum is null || m.Durum == durum)
            && (!SadeceBorclular || m.Bakiye > 0)
            && (aranan.Length == 0
                || Turkce.Kultur.CompareInfo.IndexOf(m.Unvan, aranan, System.Globalization.CompareOptions.IgnoreCase) >= 0
                || (m.Telefon?.Contains(aranan) ?? false)));

        Gorunen.Clear();
        foreach (var m in sonuc) Gorunen.Add(m);
        Ozet = $"{Gorunen.Count} mükellef · toplam bakiye {Sayi.Yaz(Gorunen.Sum(m => m.Bakiye))} ₺";
    }

    [RelayCommand]
    private void Yeni() => Ac(null);

    [RelayCommand]
    private void Devir()
    {
        if (DevirPenceresi.Ac()) Yukle();
    }

    public void Ac(int? mukellefId)
    {
        if (MukellefKartiPenceresi.Ac(mukellefId)) Yukle();
    }
}
