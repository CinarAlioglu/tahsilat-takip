using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class BorcPenceresi
{
    private BorcPenceresi(BorcModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
        Loaded += (_, _) => { if (!model.MukellefSabit) MukellefKutusu.Focus(); };
    }

    public static bool Ac(int? mukellefId = null)
    {
        var model = new BorcModeli(
            App.Servisler.GetRequiredService<TahakkukServisi>(),
            App.Servisler.GetRequiredService<MukellefServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle(mukellefId);
        return new BorcPenceresi(model) { Owner = PencereYardimcisi.Sahip() }.ShowDialog() == true;
    }
}

public partial class BorcModeli(TahakkukServisi borclar, MukellefServisi mukellefler, Oturum oturum) : ObservableObject
{
    public Action? Kapat { get; set; }
    public List<MukellefOzeti> Mukellefler { get; private set; } = [];
    public List<HizmetTuru> Hizmetler { get; private set; } = [];

    [ObservableProperty] private bool _mukellefSabit;
    [ObservableProperty] private MukellefOzeti? _seciliMukellef;
    [ObservableProperty] private HizmetTuru? _hizmet;
    [ObservableProperty] private DateTime? _tarih = DateTime.Today;
    [ObservableProperty] private string? _tutar;
    [ObservableProperty] private string? _aciklama;

    partial void OnHizmetChanged(HizmetTuru? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(Aciklama) && value.Id == HizmetTuru.DefterTasdikiId)
            Aciklama = $"{DateTime.Today.Year} yılı defter tasdiki";
    }

    public void Yukle(int? mukellefId)
    {
        Mukellefler = mukellefler.Listele(DateOnly.FromDateTime(DateTime.Today));
        Hizmetler = mukellefler.HizmetTurleri()
            .OrderBy(h => h.Id == HizmetTuru.AylikMuhasebeUcretiId).ToList(); // ek hizmetler önce
        OnPropertyChanged(nameof(Mukellefler));
        OnPropertyChanged(nameof(Hizmetler));
        Hizmet = Hizmetler.FirstOrDefault(h => h.Id == HizmetTuru.DigerId) ?? Hizmetler.FirstOrDefault();
        if (mukellefId is { } id)
        {
            SeciliMukellef = Mukellefler.FirstOrDefault(m => m.Id == id);
            MukellefSabit = SeciliMukellef is not null;
        }
    }

    [RelayCommand]
    private void Kaydet()
    {
        if (SeciliMukellef is null) throw new IslemHatasi("Listeden bir mükellef seçin.");
        if (Hizmet is null) throw new IslemHatasi("Hizmet seçin.");
        if (Tarih is null) throw new IslemHatasi("Tarih seçin.");
        var tutar = Sayi.Oku(Tutar) ?? throw new IslemHatasi("Tutarı sayı olarak girin.");
        borclar.ManuelEkle(SeciliMukellef.Id, Hizmet.Id, DateOnly.FromDateTime(Tarih.Value), tutar, Aciklama ?? "", oturum.Kullanici?.Id);
        Kapat?.Invoke();
    }
}
