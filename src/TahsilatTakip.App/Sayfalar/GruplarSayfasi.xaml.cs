using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class GruplarSayfasi
{
    public GruplarSayfasi(GruplarModeli model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }
}

public partial class GruplarModeli(GrupServisi gruplar, MukellefServisi mukellefler) : ObservableObject
{
    public ObservableCollection<GrupSatiri> Gruplar { get; } = [];
    public List<MukellefOzeti> Mukellefler { get; private set; } = [];

    [ObservableProperty] private GrupSatiri? _secili;
    [ObservableProperty] private GrupUyesi? _seciliUye;
    [ObservableProperty] private MukellefOzeti? _eklenecekMukellef;

    public void Yukle(int? seciliId = null)
    {
        seciliId ??= Secili?.Id;
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        Gruplar.Clear();
        foreach (var g in gruplar.Listele(bugun)) Gruplar.Add(g);
        Secili = Gruplar.FirstOrDefault(g => g.Id == seciliId) ?? Gruplar.FirstOrDefault();
        Mukellefler = mukellefler.Listele(bugun);
        OnPropertyChanged(nameof(Mukellefler));
    }

    private GrupSatiri SeciliGerekli() => Secili ?? throw new IslemHatasi("Önce bir grup seçin.");

    [RelayCommand]
    private void Yeni()
    {
        var ad = GirdiPenceresi.Sor("Yeni ödeme grubu", "Grubun adı (ör. MSH Grup ve şahıs firması):", "Oluştur");
        if (ad is null) return;
        Yukle(gruplar.Kaydet(null, ad, null).Id);
    }

    [RelayCommand]
    private void Adlandir()
    {
        var g = SeciliGerekli();
        var ad = GirdiPenceresi.Sor("Grubu yeniden adlandır", $"\"{g.Ad}\" için yeni ad:", "Kaydet");
        if (ad is null) return;
        gruplar.Kaydet(g.Id, ad, g.Aciklama);
        Yukle(g.Id);
    }

    [RelayCommand]
    private async Task Sil()
    {
        var g = SeciliGerekli();
        if (!await Mesaj.Onayla("Grubu sil", $"\"{g.Ad}\" grubu silinsin mi? Üye mükellefler ve kayıtları etkilenmez.", "Sil")) return;
        gruplar.Sil(g.Id);
        Yukle();
    }

    [RelayCommand]
    private void UyeEkle()
    {
        var g = SeciliGerekli();
        var m = EklenecekMukellef ?? throw new IslemHatasi("Eklenecek mükellefi seçin.");
        gruplar.UyeEkle(g.Id, m.Id);
        EklenecekMukellef = null;
        Yukle(g.Id);
    }

    [RelayCommand]
    private void UyeCikar()
    {
        var g = SeciliGerekli();
        var u = SeciliUye ?? throw new IslemHatasi("Gruptan çıkarılacak üyeyi seçin.");
        gruplar.UyeCikar(u.MukellefId);
        Yukle(g.Id);
    }

    [RelayCommand]
    private void Tahsilat()
    {
        var g = SeciliGerekli();
        if (g.Uyeler.Count == 0) throw new IslemHatasi("Grupta üye yok.");
        if (GrupTahsilatPenceresi.Ac(g.Id)) Yukle(g.Id);
    }
}
