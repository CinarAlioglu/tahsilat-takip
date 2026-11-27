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
        _model = model;
    }

    private readonly TahsilatlarModeli _model;

    private void Tablo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        _model.CokluSecim = Tablo.SelectedItems.OfType<TahsilatSatiri>().ToList();
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
    [ObservableProperty] private bool _sadeceTeslimBekleyen;
    [ObservableProperty] private string _teslimBasligi = "Sadece teslim bekleyenler";

    public List<TahsilatSatiri> CokluSecim { get; set; } = [];

    partial void OnSadeceTeslimBekleyenChanged(bool value)
    {
        if (value) { _yukleniyor = true; Baslangic = null; _yukleniyor = false; }
        Listele();
    }

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
            IptalDahil: IptalDahil,
            SadeceTeslimBekleyen: SadeceTeslimBekleyen));
        var bekleyenler = servis.Listele(new TahsilatFiltresi(SadeceTeslimBekleyen: true));
        TeslimBasligi = bekleyenler.Count > 0
            ? $"Sadece teslim bekleyenler ({bekleyenler.Count} · {Sayi.Yaz(bekleyenler.Sum(t => t.Tutar))} ₺)"
            : "Sadece teslim bekleyenler";
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
    private void TeslimAl()
    {
        var secilenler = CokluSecim.Where(t => !t.TeslimEdildi && !t.IptalEdildi).ToList();
        if (secilenler.Count == 0)
            throw new IslemHatasi("Teslim bekleyen tahsilatları seçin (Ctrl veya Shift ile birden çok satır seçebilirsiniz).");
        var alan = GirdiPenceresi.Sor("Ofise teslim",
            $"{secilenler.Count} tahsilat (toplam {Sayi.Yaz(secilenler.Sum(t => t.Tutar))} ₺) ofise teslim alındı olarak işaretlenecek. Teslim alan kişi:",
            "Teslim alındı", oturum.Kullanici?.AdSoyad);
        if (alan is null) return;
        servis.TeslimAl(secilenler.Select(t => t.Id), DateOnly.FromDateTime(DateTime.Today), alan);
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
