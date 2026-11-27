using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class AylikDurumSayfasi
{
    private readonly AylikDurumModeli _model;

    public AylikDurumSayfasi(AylikDurumModeli model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }

    private void Tablo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.FrameworkElement { DataContext: AylikDurumSatiri s } && MukellefKartiPenceresi.Ac(s.Id))
            _model.Yukle();
    }
}

public partial class AylikDurumModeli(TakipServisi takip, RaporServisi rapor) : ObservableObject
{
    private List<AylikDurumSatiri> _tumu = [];

    public List<int> Yillar { get; } = Enumerable.Range(2026, Math.Max(1, DateTime.Today.Year - 2026 + 2)).ToList();
    public ObservableCollection<AylikDurumSatiri> Satirlar { get; } = [];

    [ObservableProperty] private int _yil = DateTime.Today.Year;
    [ObservableProperty] private string _arama = "";
    [ObservableProperty] private bool _sadeceAcik;

    partial void OnYilChanged(int value) => Yukle();
    partial void OnAramaChanged(string value) => Filtrele();
    partial void OnSadeceAcikChanged(bool value) => Filtrele();

    public void Yukle()
    {
        _tumu = takip.AylikDurum(Yil, DateOnly.FromDateTime(DateTime.Today));
        Filtrele();
    }

    private void Filtrele()
    {
        var aranan = Arama.Trim();
        Satirlar.Clear();
        foreach (var s in _tumu.Where(s =>
                     (aranan.Length == 0 || Turkce.Kultur.CompareInfo.IndexOf(s.Unvan, aranan, CompareOptions.IgnoreCase) >= 0)
                     && (!SadeceAcik || s.Aylar.Any(a => a is AyDurumu.Acik or AyDurumu.Kismi) || s.OncekiKalan > 0)))
            Satirlar.Add(s);
    }

    [RelayCommand]
    private void Excel()
    {
        var dosya = OrtakIslemler.KayitYeriSor($"Aylik_odeme_durumu_{Yil}.xlsx", "Excel dosyası (*.xlsx)|*.xlsx");
        if (dosya is null) return;
        rapor.AylikDurumTablosu(Yil, DateOnly.FromDateTime(DateTime.Today), dosya);
        OrtakIslemler.Ac(dosya);
    }
}
