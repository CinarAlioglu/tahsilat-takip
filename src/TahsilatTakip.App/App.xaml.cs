using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TahsilatTakip.App.Pencereler;
using TahsilatTakip.App.Sayfalar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App;

public partial class App : Application
{
    public static IServiceProvider Servisler { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Türkçe tarih/sayı biçimleri (13.11.2026, 6.250,00).
        var tr = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = tr;
        CultureInfo.DefaultThreadCurrentUICulture = tr;
        Thread.CurrentThread.CurrentCulture = tr;
        Thread.CurrentThread.CurrentUICulture = tr;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(tr.IetfLanguageTag)));

        DispatcherUnhandledException += BeklenmeyenHata;

        // Windows koyu modda olsa da uygulama her zaman açık temayla açılır (tüm pencerelerde tutarlı görünüm).
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.Mica, true);

        try
        {
            Servisler = ServisleriKur();
            using var db = Servisler.GetRequiredService<VeritabaniFabrikasi>().Olustur();
            VeritabaniKurulumu.Hazirla(db);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Veritabanı açılamadı:\n\n" + ex.Message, "Tahsilat Takip",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        GirisGoster();
    }

    /// <summary>Giriş penceresini açar; başarılı girişte ana pencereye geçer.</summary>
    public static void GirisGoster()
    {
        var giris = Servisler.GetRequiredService<GirisPenceresi>();
        if (giris.ShowDialog() == true)
        {
            var ana = Servisler.GetRequiredService<AnaPencere>();
            Current.MainWindow = ana;
            ana.Show();
        }
        else
        {
            Current.Shutdown();
        }
    }

    private static ServiceProvider ServisleriKur()
    {
        var s = new ServiceCollection();
        var yol = new VeriYolu();
        var oturum = new Oturum();
        s.AddSingleton(yol);
        s.AddSingleton(oturum);
        s.AddSingleton(new VeritabaniFabrikasi(yol.BaglantiCumlesi, oturum));

        s.AddSingleton<KullaniciServisi>();
        s.AddSingleton<MukellefServisi>();
        s.AddSingleton<TahakkukServisi>();
        s.AddSingleton<ExcelAktarimServisi>();
        s.AddSingleton<YedekServisi>();
        s.AddSingleton<TahsilatServisi>();

        s.AddTransient<GirisPenceresi>();
        s.AddTransient<AnaPencere>();
        s.AddTransient<AnaPanelSayfasi>();
        s.AddTransient<MukelleflerSayfasi>();
        s.AddTransient<AyarlarSayfasi>();
        s.AddTransient<TahsilatlarSayfasi>();
        s.AddTransient<BorclarSayfasi>();
        s.AddTransient<TahsilatlarModeli>();
        s.AddTransient<BorclarModeli>();
        s.AddTransient<AnaPanelModeli>();
        s.AddTransient<MukelleflerModeli>();
        s.AddTransient<AyarlarModeli>();
        return s.BuildServiceProvider();
    }

    private void BeklenmeyenHata(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        var mesaj = e.Exception is IslemHatasi ? e.Exception.Message : "Beklenmeyen bir hata oluştu:\n\n" + e.Exception.Message;
        MessageBox.Show(mesaj, "Tahsilat Takip", MessageBoxButton.OK,
            e.Exception is IslemHatasi ? MessageBoxImage.Warning : MessageBoxImage.Error);
    }
}
