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

public partial class TahsilatPenceresi
{
    private TahsilatPenceresi(TahsilatModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
        model.YeniKayitaHazir = () => (model.MukellefSabit ? TutarKutusu : (System.Windows.UIElement)MukellefKutusu).Focus();
        Loaded += (_, _) => model.YeniKayitaHazir();
    }

    /// <summary>Tahsilat penceresini açar. En az bir kayıt yapıldıysa true döner.</summary>
    public static bool Ac(int? mukellefId = null)
    {
        var model = new TahsilatModeli(
            App.Servisler.GetRequiredService<TahsilatServisi>(),
            App.Servisler.GetRequiredService<MukellefServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle(mukellefId);
        new TahsilatPenceresi(model) { Owner = PencereYardimcisi.Sahip() }.ShowDialog();
        return model.KayitYapildi;
    }
}

public partial class TahsilatModeli(TahsilatServisi tahsilatlar, MukellefServisi mukellefler, Oturum oturum) : ObservableObject
{
    public Action? Kapat { get; set; }
    public Action YeniKayitaHazir { get; set; } = () => { };
    public bool KayitYapildi { get; private set; }

    public List<MukellefOzeti> Mukellefler { get; private set; } = [];
    public List<Secenek<OdemeYontemi>> Yontemler => Secenekler.OdemeYontemleri;
    public List<string> TahsilEdenler { get; private set; } = [];
    public List<string> Hesaplar { get; private set; } = [];

    [ObservableProperty] private bool _mukellefSabit;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BakiyeMetni))] private MukellefOzeti? _seciliMukellef;
    [ObservableProperty] private DateTime? _tarih = DateTime.Today;
    [ObservableProperty] private string? _tutar;
    [ObservableProperty] private Secenek<OdemeYontemi> _yontem = Secenekler.OdemeYontemleri[1];
    [ObservableProperty] private string? _tahsilEden;
    [ObservableProperty] private string? _hesap;
    [ObservableProperty] private string? _odeyenKisi;
    [ObservableProperty] private string? _aciklama;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DekontAdi))] private string? _dekont;

    public string DekontAdi => Dekont is null ? "Seçilmedi" : Path.GetFileName(Dekont);

    public string BakiyeMetni => SeciliMukellef switch
    {
        null => "",
        { Bakiye: > 0 } m => $"Güncel borç: {Sayi.Yaz(m.Bakiye)} ₺" + (m.GecikmeGunu > 0 ? $" · {m.GecikmeGunu} gün gecikmede" : ""),
        { Bakiye: < 0 } m => $"Fazla ödeme (avans): {Sayi.Yaz(-m.Bakiye)} ₺",
        _ => "Borcu yok",
    };

    public void Yukle(int? mukellefId)
    {
        var tumu = mukellefler.Listele(DateOnly.FromDateTime(DateTime.Today));
        // Ayrılanlar da listede kalsın (eski borcunu ödeyebilir) ama sonda görünsün.
        Mukellefler = tumu.OrderBy(m => m.Durum == MukellefDurumu.Ayrildi).ToList();
        TahsilEdenler = tahsilatlar.TahsilEdenler();
        Hesaplar = tahsilatlar.Hesaplar();
        OnPropertyChanged(nameof(Mukellefler));
        OnPropertyChanged(nameof(TahsilEdenler));
        OnPropertyChanged(nameof(Hesaplar));
        if (mukellefId is { } id)
        {
            SeciliMukellef = Mukellefler.FirstOrDefault(m => m.Id == id);
            MukellefSabit = SeciliMukellef is not null;
        }
    }

    [RelayCommand]
    private void DekontSec()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Dekont seçin",
            Filter = "Dekont (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png|Tüm dosyalar (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) Dekont = dlg.FileName;
    }

    [RelayCommand]
    private void DekontKaldir() => Dekont = null;

    [RelayCommand]
    private void Kaydet(string? devam)
    {
        if (SeciliMukellef is null) throw new IslemHatasi("Listeden bir mükellef seçin.");
        if (Tarih is null) throw new IslemHatasi("Tarih seçin.");
        var tutar = Sayi.Oku(Tutar) ?? throw new IslemHatasi("Tutarı sayı olarak girin (ör. 6.250).");

        tahsilatlar.Ekle(new TahsilatGirdisi(SeciliMukellef.Id, DateOnly.FromDateTime(Tarih.Value), tutar, Yontem.Deger,
            TahsilEden, Hesap, OdeyenKisi, Aciklama, Dekont), oturum.Kullanici?.Id);
        KayitYapildi = true;

        if (devam == "yeni")
        {
            // Tarih, yöntem, tahsil eden ve hesap aynı kalır; seri giriş için.
            var tahsilEden = TahsilEden;
            var hesap = Hesap;
            Yukle(null);
            TahsilEden = tahsilEden;
            Hesap = hesap;
            SeciliMukellef = null;
            Tutar = OdeyenKisi = Aciklama = Dekont = null;
            YeniKayitaHazir();
        }
        else
        {
            Kapat?.Invoke();
        }
    }
}
