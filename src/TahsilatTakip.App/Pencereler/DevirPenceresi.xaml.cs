using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class DevirPenceresi
{
    private DevirPenceresi(DevirModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
    }

    public static bool Ac()
    {
        var model = new DevirModeli(
            App.Servisler.GetRequiredService<MukellefServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle();
        return new DevirPenceresi(model) { Owner = PencereYardimcisi.Sahip() }.ShowDialog() == true;
    }
}

public partial class DevirSatiri(DevirModeli sahip) : ObservableObject
{
    public int Id { get; init; }
    public string Unvan { get; init; } = "";
    public string? Notlar { get; init; }
    public decimal MevcutDevir { get; init; }

    [ObservableProperty] private string? _tutar;

    partial void OnTutarChanged(string? value) => sahip.OzetGuncelle();
}

public partial class DevirModeli(MukellefServisi servis, Oturum oturum) : ObservableObject
{
    private List<DevirSatiri> _tumu = [];

    public Action? Kapat { get; set; }
    public ObservableCollection<DevirSatiri> Gorunen { get; } = [];

    [ObservableProperty] private DateTime? _tarih =
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
    [ObservableProperty] private bool _girilenleriGizle = true;
    [ObservableProperty] private string _ozet = "";

    partial void OnGirilenleriGizleChanged(bool value) => Filtrele();

    public void Yukle()
    {
        _tumu = servis.DevirListesi()
            .Select(x => new DevirSatiri(this) { Id = x.Id, Unvan = x.Unvan, Notlar = x.Notlar, MevcutDevir = x.MevcutDevir })
            .ToList();
        Filtrele();
    }

    private void Filtrele()
    {
        Gorunen.Clear();
        foreach (var s in _tumu.Where(s => !GirilenleriGizle || s.MevcutDevir == 0 || !string.IsNullOrWhiteSpace(s.Tutar)))
            Gorunen.Add(s);
        OzetGuncelle();
    }

    public void OzetGuncelle()
    {
        var dolu = _tumu.Where(s => !string.IsNullOrWhiteSpace(s.Tutar)).ToList();
        var hatali = dolu.Count(s => Sayi.Oku(s.Tutar) is null);
        Ozet = $"{dolu.Count} mükellefe toplam {Sayi.Yaz(dolu.Sum(s => Sayi.Oku(s.Tutar) ?? 0))} ₺ devir girilecek"
               + (hatali > 0 ? $" · {hatali} satırda tutar okunamadı" : "");
    }

    [RelayCommand]
    private async Task Kaydet()
    {
        if (Tarih is null) throw new IslemHatasi("Devir tarihini seçin.");
        var dolu = _tumu.Where(s => !string.IsNullOrWhiteSpace(s.Tutar)).ToList();
        if (dolu.Count == 0) throw new IslemHatasi("Hiçbir satıra tutar yazılmadı.");
        var hatali = dolu.FirstOrDefault(s => Sayi.Oku(s.Tutar) is null);
        if (hatali is not null) throw new IslemHatasi($"{hatali.Unvan} satırındaki tutar okunamadı: \"{hatali.Tutar}\"");
        var tekrar = dolu.Where(s => s.MevcutDevir > 0).Select(s => s.Unvan).ToList();

        var mesaj = $"{dolu.Count} mükellefe {Tarih:dd.MM.yyyy} tarihli toplam {Sayi.Yaz(dolu.Sum(s => Sayi.Oku(s.Tutar)!.Value))} ₺ devir girilecek.";
        if (tekrar.Count > 0) mesaj += $"\n\nDikkat: {string.Join(", ", tekrar)} için daha önce devir girilmiş; yeni tutar ona eklenecek.";
        if (!await Mesaj.Onayla("Devirleri kaydet", mesaj, "Kaydet")) return;

        servis.TopluDevirGir(dolu.Select(s => (s.Id, Sayi.Oku(s.Tutar)!.Value)), DateOnly.FromDateTime(Tarih.Value), oturum.Kullanici?.Id);
        Kapat?.Invoke();
    }
}
