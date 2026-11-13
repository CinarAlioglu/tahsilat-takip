using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Sayfalar;

public partial class AnaPanelSayfasi
{
    public AnaPanelSayfasi(AnaPanelModeli model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }
}

public sealed record BekleyenBorcSatiri(string Unvan, string Aciklama, DateOnly Tarih, decimal Tutar);

public partial class AnaPanelModeli(
    MukellefServisi mukellefler, TahakkukServisi tahakkuklar, YedekServisi yedek, Oturum oturum) : ObservableObject
{
    private List<TahakkukOnerisi> _oneriler = [];

    [ObservableProperty] private int _aktifMukellef;
    [ObservableProperty] private decimal _toplamAlacak;
    [ObservableProperty] private int _gecikenSayisi;
    [ObservableProperty] private decimal _toplamAvans;
    [ObservableProperty] private bool _bekleyenVar;
    [ObservableProperty] private string _bekleyenOzeti = "";
    [ObservableProperty] private string _yedekBilgisi = "";

    public ObservableCollection<BekleyenBorcSatiri> BekleyenBorclar { get; } = [];
    public ObservableCollection<MukellefOzeti> EnYuksekBakiyeler { get; } = [];

    public void Yukle()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var liste = mukellefler.Listele(bugun);
        AktifMukellef = liste.Count(m => m.Durum == MukellefDurumu.Aktif);
        ToplamAlacak = liste.Where(m => m.Bakiye > 0).Sum(m => m.Bakiye);
        ToplamAvans = liste.Where(m => m.Bakiye < 0).Sum(m => -m.Bakiye);
        GecikenSayisi = liste.Count(m => m.Bakiye > 0 && m.GecikmeGunu > 0);

        EnYuksekBakiyeler.Clear();
        foreach (var m in liste.Where(m => m.Bakiye > 0).OrderByDescending(m => m.Bakiye).Take(10))
            EnYuksekBakiyeler.Add(m);

        var unvanlar = liste.ToDictionary(m => m.Id, m => m.Unvan);
        _oneriler = tahakkuklar.Onizle(bugun);
        BekleyenBorclar.Clear();
        foreach (var o in _oneriler)
            BekleyenBorclar.Add(new BekleyenBorcSatiri(unvanlar.GetValueOrDefault(o.MukellefId, "?"), o.Aciklama, o.Tarih, o.Tutar));
        BekleyenVar = _oneriler.Count > 0;
        BekleyenOzeti = BekleyenVar
            ? $"{_oneriler.Select(o => o.MukellefId).Distinct().Count()} mükellef için {_oneriler.Count} borç, toplam {Sayi.Yaz(_oneriler.Sum(o => o.Tutar))} ₺. Kontrol edip onaylayın."
            : "Tüm dönemlerin borçları yazılmış. Bir sonraki ay başında yeni dönemin borçları burada görünecek.";

        var son = yedek.SonYedekTarihi();
        YedekBilgisi = son is null ? "Henüz yedek alınmadı." : $"Son yedek: {son:dd.MM.yyyy HH:mm}";
    }

    [RelayCommand]
    private void TahsilatAl()
    {
        if (Pencereler.TahsilatPenceresi.Ac()) Yukle();
    }

    [RelayCommand]
    private async Task BorclariYaz()
    {
        if (_oneriler.Count == 0) return;
        var onay = await Mesaj.Onayla("Borçları yaz",
            $"{_oneriler.Count} borç kaydı oluşturulacak (toplam {Sayi.Yaz(_oneriler.Sum(o => o.Tutar))} ₺). Onaylıyor musunuz?",
            "Borçları yaz");
        if (!onay) return;
        var sayi = tahakkuklar.Olustur(_oneriler, oturum.Kullanici?.Id);
        Yukle();
        await Mesaj.Bilgi("Tamamlandı", $"{sayi} borç kaydı oluşturuldu.");
    }
}
