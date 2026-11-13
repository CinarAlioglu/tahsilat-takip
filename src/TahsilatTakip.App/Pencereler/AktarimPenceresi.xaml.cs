using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class AktarimPenceresi
{
    private AktarimPenceresi(AktarimModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
    }

    public static bool Ac()
    {
        var model = new AktarimModeli(App.Servisler.GetRequiredService<ExcelAktarimServisi>());
        var pencere = new AktarimPenceresi(model) { Owner = Application.Current.MainWindow };
        return pencere.ShowDialog() == true;
    }
}

public partial class AktarimModeli : ObservableObject
{
    private readonly ExcelAktarimServisi _servis;

    public AktarimModeli(ExcelAktarimServisi servis)
    {
        _servis = servis;
        var buAy = Donem.AyBasi(DateOnly.FromDateTime(DateTime.Today));
        _gecisAyi = Aylar.First(a => a.Ay == buAy);
        GecisAciklamasiniGuncelle();
    }

    public Action? Kapat { get; set; }
    public List<AySecenegi> Aylar { get; } = AySecenegi.Liste(12, 12);
    public ObservableCollection<AktarimSatiri> Satirlar { get; } = [];

    [ObservableProperty] private string _dosyaAdi = "Dosya seçilmedi";
    [ObservableProperty] private AySecenegi _gecisAyi;
    [ObservableProperty] private string _gecisAciklamasi = "";
    [ObservableProperty] private string _ozet = "";

    partial void OnGecisAyiChanged(AySecenegi value) => GecisAciklamasiniGuncelle();

    private void GecisAciklamasiniGuncelle()
    {
        var ay = GecisAyi.Ay;
        var onceki = ay.AddDays(-1);
        GecisAciklamasi =
            $"Aylık ücretler {GecisAyi.Ad} ayından itibaren borçlanır (ilk borç {Donem.BorcTarihi(Core.Varliklar.UcretPeriyodu.Aylik, ay):dd.MM.yyyy}). " +
            $"3 aylık ve yıllık ücretler bu ayı içeren dönemin tamamı için dönem sonunda borçlanır. " +
            $"Devirleri {onceki:dd.MM.yyyy} itibarıyla girin.";
    }

    [RelayCommand]
    private void DosyaSec()
    {
        var dlg = new OpenFileDialog { Filter = "Excel dosyaları (*.xlsx)|*.xlsx", Title = "Mükellef listesi seçin" };
        if (dlg.ShowDialog() != true) return;
        DosyaAdi = Path.GetFileName(dlg.FileName);
        Satirlar.Clear();
        foreach (var s in _servis.Oku(dlg.FileName)) Satirlar.Add(s);
        Ozet = $"{Satirlar.Count} satır okundu · {Satirlar.Count(s => s.Secili)} aktarılacak · " +
               $"{Satirlar.Count(s => s.Secili && (s.Periyot is null || s.Ucret is null))} tanesinde ücret planı eksik (sonradan girilebilir)";
    }

    [RelayCommand]
    private async Task Aktar()
    {
        var secili = Satirlar.Where(s => s.Secili).ToList();
        if (secili.Count == 0) throw new IslemHatasi("Aktarılacak satır yok. Önce dosya seçin.");
        if (!await Mesaj.Onayla("Aktarım", $"{secili.Count} mükellef aktarılacak. Devam edilsin mi?", "Aktar")) return;
        var sonuc = _servis.Aktar(Satirlar, GecisAyi.Ay);
        await Mesaj.Bilgi("Aktarım tamamlandı",
            $"{sonuc.EklenenMukellef} mükellef ve {sonuc.EklenenPlan} ücret planı eklendi. {sonuc.Atlanan} satır atlandı.");
        Kapat?.Invoke();
    }
}
