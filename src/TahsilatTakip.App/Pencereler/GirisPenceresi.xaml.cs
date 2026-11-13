using System.Windows;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

/// <summary>
/// Giriş ekranı. Veritabanında hiç kullanıcı yoksa "ilk kurulum" kipinde açılır ve yönetici hesabı oluşturur.
/// </summary>
public partial class GirisPenceresi
{
    private readonly KullaniciServisi _kullanicilar;
    private readonly bool _ilkKurulum;

    public GirisPenceresi(KullaniciServisi kullanicilar)
    {
        _kullanicilar = kullanicilar;
        InitializeComponent();

        _ilkKurulum = !kullanicilar.KullaniciVarMi();
        if (_ilkKurulum)
        {
            BaslikMetni.Text = "İlk kurulum";
            AciklamaMetni.Text = "Uygulamayı ilk kez açıyorsunuz. Yönetici hesabınızı oluşturun; diğer kullanıcıları daha sonra Ayarlar'dan ekleyebilirsiniz.";
            KurulumAlanlari.Visibility = Visibility.Visible;
            SifreTekrarAlani.Visibility = Visibility.Visible;
            GirisDugmesi.Content = "Hesabı oluştur ve giriş yap";
        }
        Loaded += (_, _) => (_ilkKurulum ? (UIElement)AdSoyadKutusu : KullaniciAdiKutusu).Focus();
    }

    private void Giris_Click(object sender, RoutedEventArgs e)
    {
        HataCubugu.IsOpen = false;
        try
        {
            if (_ilkKurulum)
            {
                if (SifreKutusu.Password != SifreTekrarKutusu.Password)
                    throw new IslemHatasi("Şifreler birbirini tutmuyor.");
                _kullanicilar.Olustur(KullaniciAdiKutusu.Text, AdSoyadKutusu.Text, SifreKutusu.Password, KullaniciRolu.Yonetici);
            }

            if (!_kullanicilar.GirisYap(KullaniciAdiKutusu.Text, SifreKutusu.Password))
                throw new IslemHatasi("Kullanıcı adı veya şifre hatalı.");

            DialogResult = true;
        }
        catch (IslemHatasi ex)
        {
            HataCubugu.Message = ex.Message;
            HataCubugu.IsOpen = true;
            SifreKutusu.Clear();
            SifreKutusu.Focus();
        }
    }
}
