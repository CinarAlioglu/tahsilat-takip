using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class TahsilatlarSayfasi
{
    public TahsilatlarSayfasi(TahsilatlarModeli model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }
}

public partial class TahsilatlarModeli(TahsilatServisi servis, Oturum oturum) : ObservableObject
{
    private const string Tumu = "Tümü";
    private bool _yukleniyor;

    public ObservableCollection<TahsilatSatiri> Satirlar { get; } = [];
    public ObservableCollection<string> TahsilEdenler { get; } = [];

    [ObservableProperty] private DateTime? _baslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
    [ObservableProperty] private DateTime? _bitis = DateTime.Today;
    [ObservableProperty] private string? _tahsilEden = Tumu;
    [ObservableProperty] private bool _iptalDahil;
    [ObservableProperty] private TahsilatSatiri? _secili;
    [ObservableProperty] private string _ozet = "";

    partial void OnBaslangicChanged(DateTime? value) => Listele();
    partial void OnBitisChanged(DateTime? value) => Listele();
    partial void OnTahsilEdenChanged(string? value) => Listele();
    partial void OnIptalDahilChanged(bool value) => Listele();

    public void Yukle()
    {
        _yukleniyor = true;
        var secili = TahsilEden;
        TahsilEdenler.Clear();
        TahsilEdenler.Add(Tumu);
        foreach (var t in servis.TahsilEdenler()) TahsilEdenler.Add(t);
        TahsilEden = TahsilEdenler.Contains(secili ?? "") ? secili : Tumu;
        _yukleniyor = false;
        Listele();
    }

    private void Listele()
    {
        if (_yukleniyor) return;
        var liste = servis.Listele(new TahsilatFiltresi(
            Baslangic is { } b ? DateOnly.FromDateTime(b) : null,
            Bitis is { } s ? DateOnly.FromDateTime(s) : null,
            TahsilEden: TahsilEden == Tumu ? null : TahsilEden,
            IptalDahil: IptalDahil));
        Satirlar.Clear();
        foreach (var t in liste) Satirlar.Add(t);

        var gecerli = liste.Where(t => !t.IptalEdildi).ToList();
        var dagilim = gecerli.GroupBy(t => string.IsNullOrWhiteSpace(t.TahsilEden) ? "Belirtilmemiş" : t.TahsilEden!)
            .OrderByDescending(g => g.Sum(t => t.Tutar))
            .Select(g => $"{g.Key}: {Sayi.Yaz(g.Sum(t => t.Tutar))} ₺");
        Ozet = $"{gecerli.Count} tahsilat · toplam {Sayi.Yaz(gecerli.Sum(t => t.Tutar))} ₺"
               + (gecerli.Count > 0 ? "   (" + string.Join(" · ", dagilim) + ")" : "");
    }

    [RelayCommand]
    private void Yeni()
    {
        if (TahsilatPenceresi.Ac()) Yukle();
    }

    [RelayCommand]
    private void Iptal()
    {
        if (Secili is null) throw new IslemHatasi("Önce listeden bir tahsilat seçin.");
        if (Secili.IptalEdildi) throw new IslemHatasi("Bu tahsilat zaten iptal edilmiş.");
        var neden = GirdiPenceresi.Sor("Tahsilatı iptal et",
            $"{Secili.Tarih:dd.MM.yyyy} tarihli, {Secili.Unvan} adına {Sayi.Yaz(Secili.Tutar)} ₺ tahsilat iptal edilecek. İptal nedenini yazın:",
            "İptal et");
        if (neden is null) return;
        servis.IptalEt(Secili.Id, neden, oturum.Kullanici?.Id);
        Listele();
    }

    [RelayCommand]
    private void DekontAc(TahsilatSatiri? satir)
    {
        var yol = servis.DekontTamYolu(satir?.DekontDosyasi);
        if (yol is null || !File.Exists(yol)) throw new IslemHatasi("Dekont dosyası bulunamadı.");
        Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true });
    }
}
