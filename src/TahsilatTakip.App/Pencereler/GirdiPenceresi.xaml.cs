using System.Windows;

namespace TahsilatTakip.App.Pencereler;

/// <summary>Tek satırlık metin isteyen küçük pencere (ör. iptal nedeni).</summary>
public partial class GirdiPenceresi
{
    private GirdiPenceresi(string baslik, string aciklama, string dugme)
    {
        InitializeComponent();
        Title = baslik;
        Baslik.Title = baslik;
        Aciklama.Text = aciklama;
        Tamam.Content = dugme;
        Loaded += (_, _) => Girdi.Focus();
    }

    /// <summary>Kullanıcı vazgeçerse veya boş bırakırsa null döner.</summary>
    public static string? Sor(string baslik, string aciklama, string dugme = "Tamam")
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        var p = new GirdiPenceresi(baslik, aciklama, dugme) { Owner = owner };
        return p.ShowDialog() == true && !string.IsNullOrWhiteSpace(p.Girdi.Text) ? p.Girdi.Text.Trim() : null;
    }

    private void Tamam_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
