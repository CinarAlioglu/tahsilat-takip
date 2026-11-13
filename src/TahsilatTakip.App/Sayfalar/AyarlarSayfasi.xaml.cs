using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;
using Wpf.Ui.Appearance;

namespace TahsilatTakip.App.Sayfalar;

public partial class AyarlarSayfasi
{
    private readonly AyarlarModeli _model;

    public AyarlarSayfasi(AyarlarModeli model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.Yukle();
    }

    // Şifre kutuları güvenlik gereği bağlanamadığı için düğmeler arka koddan çağrılır.
    private async void KullaniciEkle_Click(object sender, RoutedEventArgs e)
    {
        if (await Calistir(() => _model.KullaniciEkle(YeniSifre.Password))) YeniSifre.Clear();
    }

    private async void SifreSifirla_Click(object sender, RoutedEventArgs e)
    {
        if (await Calistir(() => _model.SifreSifirla(SifirlamaSifresi.Password))) SifirlamaSifresi.Clear();
    }

    private async void SifreDegistir_Click(object sender, RoutedEventArgs e)
    {
        if (YeniSifre1.Password != YeniSifre2.Password)
        {
            await Mesaj.Bilgi("Şifre", "Yeni şifreler birbirini tutmuyor.");
            return;
        }
        if (await Calistir(() => _model.SifremiDegistir(EskiSifre.Password, YeniSifre1.Password)))
        {
            EskiSifre.Clear(); YeniSifre1.Clear(); YeniSifre2.Clear();
            await Mesaj.Bilgi("Şifre", "Şifreniz değiştirildi.");
        }
    }

    private static async Task<bool> Calistir(Action islem)
    {
        try { islem(); return true; }
        catch (IslemHatasi ex) { await Mesaj.Bilgi("Uyarı", ex.Message); return false; }
    }
}

public partial class AyarlarModeli(
    KullaniciServisi kullanicilar, YedekServisi yedek, VeriYolu yol, Oturum oturum) : ObservableObject
{
    public ObservableCollection<Kullanici> Kullanicilar { get; } = [];
    public List<Secenek<KullaniciRolu>> Roller => Secenekler.Roller;
    public bool Yonetici => oturum.YoneticiMi;
    public string VeriKlasoru => yol.Kok;
    public string Surum => $"Tahsilat Takip {typeof(App).Assembly.GetName().Version?.ToString(3)}";

    [ObservableProperty] private Kullanici? _seciliKullanici;
    [ObservableProperty] private string? _yeniAdSoyad;
    [ObservableProperty] private string? _yeniKullaniciAdi;
    [ObservableProperty] private Secenek<KullaniciRolu> _yeniRol = Secenekler.Roller[0];
    [ObservableProperty] private string _sonYedek = "";
    [ObservableProperty] private string? _ekKlasor;
    [ObservableProperty] private bool _koyuTema = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    partial void OnKoyuTemaChanged(bool value) =>
        ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);

    public void Yukle()
    {
        Kullanicilar.Clear();
        foreach (var k in kullanicilar.Listele()) Kullanicilar.Add(k);
        EkKlasor = yedek.EkKlasor();
        SonYedekGuncelle();
    }

    private void SonYedekGuncelle()
    {
        var son = yedek.SonYedekTarihi();
        SonYedek = son is null ? "Henüz yedek alınmadı." : $"Son yedek: {son:dd.MM.yyyy HH:mm}";
    }

    [RelayCommand]
    private void AktarimAc() => AktarimPenceresi.Ac();

    public void KullaniciEkle(string sifre)
    {
        kullanicilar.Olustur(YeniKullaniciAdi ?? "", YeniAdSoyad ?? "", sifre, YeniRol.Deger);
        YeniAdSoyad = YeniKullaniciAdi = null;
        Yukle();
    }

    public void SifreSifirla(string yeniSifre)
    {
        if (SeciliKullanici is null) throw new IslemHatasi("Önce listeden bir kullanıcı seçin.");
        if (SeciliKullanici.Id == oturum.Kullanici?.Id)
            throw new IslemHatasi("Kendi şifrenizi 'Şifremi değiştir' bölümünden değiştirin.");
        kullanicilar.SifreDegistir(SeciliKullanici.Id, null, yeniSifre);
    }

    public void SifremiDegistir(string eski, string yeni) =>
        kullanicilar.SifreDegistir(oturum.Kullanici!.Id, eski, yeni);

    [RelayCommand]
    private void AktiflikDegistir()
    {
        if (SeciliKullanici is null) throw new IslemHatasi("Önce listeden bir kullanıcı seçin.");
        kullanicilar.AktiflikDegistir(SeciliKullanici.Id, !SeciliKullanici.Aktif);
        Yukle();
    }

    [RelayCommand]
    private async Task Yedekle()
    {
        var dosyalar = yedek.KapanisYedegi();
        SonYedekGuncelle();
        await Mesaj.Bilgi("Yedekleme", "Yedek alındı:\n" + string.Join("\n", dosyalar));
    }

    [RelayCommand]
    private void KlasoruAc() => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{yol.Kok}\"") { UseShellExecute = true });

    [RelayCommand]
    private void EkKlasorSec()
    {
        var dlg = new OpenFolderDialog { Title = "Ek yedek klasörünü seçin" };
        if (dlg.ShowDialog() == true) EkKlasor = dlg.FolderName;
    }

    [RelayCommand]
    private async Task EkKlasorKaydet()
    {
        var klasor = string.IsNullOrWhiteSpace(EkKlasor) ? null : EkKlasor.Trim();
        if (klasor is not null && !System.IO.Directory.Exists(klasor))
            throw new IslemHatasi("Klasör bulunamadı.");
        yedek.EkKlasorKaydet(klasor);
        await Mesaj.Bilgi("Yedekleme", klasor is null ? "Ek yedek klasörü kaldırıldı." : "Ek yedek klasörü kaydedildi.");
    }
}
