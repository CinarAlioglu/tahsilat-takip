using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class RaporlarSayfasi
{
    public RaporlarSayfasi(RaporlarModeli model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }
}

public partial class RaporlarModeli(RaporServisi rapor, MukellefServisi mukellefler) : ObservableObject
{
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);

    public List<int> Yillar { get; } = Enumerable.Range(2026, Math.Max(1, DateTime.Today.Year - 2026 + 2)).ToList();
    public List<MukellefOzeti> Mukellefler { get; private set; } = [];

    [ObservableProperty] private DateTime? _baslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _bitis = DateTime.Today;
    [ObservableProperty] private int _yil = DateTime.Today.Year;
    [ObservableProperty] private MukellefOzeti? _ekstreMukellefi;

    public void Yukle()
    {
        Mukellefler = mukellefler.Listele(Bugun);
        OnPropertyChanged(nameof(Mukellefler));
    }

    [RelayCommand]
    private void TahsilatRaporu()
    {
        if (Baslangic is null || Bitis is null) throw new IslemHatasi("Tarih aralığını seçin.");
        if (Bitis < Baslangic) throw new IslemHatasi("Bitiş tarihi başlangıçtan önce olamaz.");
        var dosya = OrtakIslemler.KayitYeriSor($"Tahsilat_raporu_{Baslangic:yyyyMMdd}_{Bitis:yyyyMMdd}.xlsx", "Excel dosyası (*.xlsx)|*.xlsx");
        if (dosya is null) return;
        rapor.TahsilatRaporu(DateOnly.FromDateTime(Baslangic.Value), DateOnly.FromDateTime(Bitis.Value), dosya);
        OrtakIslemler.Ac(dosya);
    }

    [RelayCommand]
    private void BakiyeListesi()
    {
        var dosya = OrtakIslemler.KayitYeriSor($"Bakiye_listesi_{DateTime.Today:yyyyMMdd}.xlsx", "Excel dosyası (*.xlsx)|*.xlsx");
        if (dosya is null) return;
        rapor.BakiyeListesi(Bugun, dosya);
        OrtakIslemler.Ac(dosya);
    }

    [RelayCommand]
    private void AylikDurum()
    {
        var dosya = OrtakIslemler.KayitYeriSor($"Aylik_odeme_durumu_{Yil}.xlsx", "Excel dosyası (*.xlsx)|*.xlsx");
        if (dosya is null) return;
        rapor.AylikDurumTablosu(Yil, Bugun, dosya);
        OrtakIslemler.Ac(dosya);
    }

    [RelayCommand]
    private void Ekstre()
    {
        var m = EkstreMukellefi ?? throw new IslemHatasi("Mükellef seçin.");
        OrtakIslemler.EkstrePdf(m.Id, m.Unvan);
    }
}
