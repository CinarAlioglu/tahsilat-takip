using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class GrupTahsilatPenceresi
{
    private GrupTahsilatPenceresi(GrupTahsilatModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
        Loaded += (_, _) => TutarKutusu.Focus();
    }

    public static bool Ac(int grupId)
    {
        var model = new GrupTahsilatModeli(
            App.Servisler.GetRequiredService<GrupServisi>(),
            App.Servisler.GetRequiredService<TahsilatServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle(grupId);
        return new GrupTahsilatPenceresi(model) { Owner = PencereYardimcisi.Sahip() }.ShowDialog() == true;
    }
}

public partial class GrupTahsilatModeli(GrupServisi gruplar, TahsilatServisi tahsilatlar, Oturum oturum) : ObservableObject
{
    private int _grupId;

    public Action? Kapat { get; set; }
    public List<Secenek<OdemeYontemi>> Yontemler => Secenekler.OdemeYontemleri;
    public List<string> TahsilEdenler { get; private set; } = [];
    public List<string> Hesaplar { get; private set; } = [];
    public ObservableCollection<GrupDagilimi> Dagilim { get; } = [];

    [ObservableProperty] private string _grupAdi = "";
    [ObservableProperty] private string _bakiyeMetni = "";
    [ObservableProperty] private DateTime? _tarih = DateTime.Today;
    [ObservableProperty] private string? _tutar;
    [ObservableProperty] private Secenek<OdemeYontemi> _yontem = Secenekler.OdemeYontemleri[1];
    [ObservableProperty] private string? _tahsilEden;
    [ObservableProperty] private string? _hesap;
    [ObservableProperty] private string? _odeyenKisi;
    [ObservableProperty] private string? _aciklama;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DekontAdi))] private string? _dekont;

    public string DekontAdi => Dekont is null ? "Seçilmedi" : Path.GetFileName(Dekont);

    partial void OnTutarChanged(string? value)
    {
        Dagilim.Clear();
        if (Sayi.Oku(value) is { } t && t > 0)
            foreach (var d in gruplar.Dagit(_grupId, t)) Dagilim.Add(d);
    }

    public void Yukle(int grupId)
    {
        _grupId = grupId;
        var g = gruplar.Listele(DateOnly.FromDateTime(DateTime.Today)).First(x => x.Id == grupId);
        GrupAdi = g.Ad + " · " + g.UyeMetni;
        BakiyeMetni = $"Grubun toplam borcu: {Sayi.Yaz(g.ToplamBakiye)} ₺";
        TahsilEdenler = tahsilatlar.TahsilEdenler();
        Hesaplar = tahsilatlar.Hesaplar();
        OnPropertyChanged(nameof(TahsilEdenler));
        OnPropertyChanged(nameof(Hesaplar));
    }

    [RelayCommand]
    private void DekontSec()
    {
        var dlg = new OpenFileDialog { Title = "Dekont seçin", Filter = "Dekont (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png|Tüm dosyalar (*.*)|*.*" };
        if (dlg.ShowDialog() == true) Dekont = dlg.FileName;
    }

    [RelayCommand]
    private void Kaydet()
    {
        if (Tarih is null) throw new IslemHatasi("Tarih seçin.");
        var tutar = Sayi.Oku(Tutar) ?? throw new IslemHatasi("Tutarı sayı olarak girin.");
        gruplar.TahsilatAl(_grupId, new TahsilatGirdisi(0, DateOnly.FromDateTime(Tarih.Value), tutar, Yontem.Deger,
            TahsilEden, Hesap, OdeyenKisi, Aciklama, Dekont), oturum.Kullanici?.Id);
        Kapat?.Invoke();
    }
}
