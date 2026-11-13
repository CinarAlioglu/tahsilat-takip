using System.ComponentModel;
using System.Windows;
using TahsilatTakip.App.Sayfalar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class AnaPencere
{
    private readonly Oturum _oturum;
    private readonly YedekServisi _yedek;
    private bool _cikisYapiliyor;

    public AnaPencere(Oturum oturum, YedekServisi yedek)
    {
        _oturum = oturum;
        _yedek = yedek;
        InitializeComponent();

        Gezinti.SetServiceProvider(App.Servisler);
        KullaniciMetni.Text = oturum.Kullanici?.AdSoyad ?? "";
        Loaded += (_, _) => Gezinti.Navigate(typeof(AnaPanelSayfasi));
    }

    private void Cikis_Click(object sender, RoutedEventArgs e)
    {
        _cikisYapiliyor = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        try { _yedek.KapanisYedegi(); }
        catch (Exception ex)
        {
            MessageBox.Show("Kapanış yedeği alınamadı:\n" + ex.Message, "Tahsilat Takip",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _oturum.Kapat();
        if (_cikisYapiliyor) App.GirisGoster();
        else Application.Current.Shutdown();
    }
}
