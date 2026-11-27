using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Kontroller;

/// <summary>
/// Mükellef seçimi: ünvanın herhangi bir yerinde geçen metinle arar (ör. "başkır" yazınca "RAMAZAN BAŞKIR" bulunur).
/// ↓/↑ ile listede gezilir, Enter ile seçilir.
/// </summary>
public partial class MukellefSecici
{
    public static readonly DependencyProperty MukelleflerProperty = DependencyProperty.Register(
        nameof(Mukellefler), typeof(IEnumerable<MukellefOzeti>), typeof(MukellefSecici), new PropertyMetadata(null));

    public static readonly DependencyProperty SeciliProperty = DependencyProperty.Register(
        nameof(Secili), typeof(MukellefOzeti), typeof(MukellefSecici),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SeciliDegisti));

    private bool _metinAyarlaniyor;
    private bool _yaziliyor;

    public MukellefSecici() => InitializeComponent();

    public IEnumerable<MukellefOzeti>? Mukellefler
    {
        get => (IEnumerable<MukellefOzeti>?)GetValue(MukelleflerProperty);
        set => SetValue(MukelleflerProperty, value);
    }

    public MukellefOzeti? Secili
    {
        get => (MukellefOzeti?)GetValue(SeciliProperty);
        set => SetValue(SeciliProperty, value);
    }

    public new bool Focus() => Kutu.Focus();

    private static void SeciliDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (MukellefSecici)d;
        var yeni = (e.NewValue as MukellefOzeti)?.Unvan ?? "";
        if (s._yaziliyor || s.Kutu.Text == yeni) return;
        s._metinAyarlaniyor = true;
        s.Kutu.Text = yeni;
        s.Kutu.CaretIndex = yeni.Length;
        s._metinAyarlaniyor = false;
    }

    private void Kutu_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_metinAyarlaniyor) return;
        var aranan = Kutu.Text.Trim();
        if (Secili is not null && Secili.Unvan != Kutu.Text)
        {
            _yaziliyor = true;
            Secili = null;
            _yaziliyor = false;
        }
        if (aranan.Length == 0 || Mukellefler is null)
        {
            Acilir.IsOpen = false;
            return;
        }
        var bulunan = Mukellefler
            .Where(m => Turkce.Kultur.CompareInfo.IndexOf(m.Unvan, aranan, CompareOptions.IgnoreCase) >= 0)
            .OrderBy(m => Turkce.Kultur.CompareInfo.IsPrefix(m.Unvan, aranan, CompareOptions.IgnoreCase) ? 0 : 1)
            .Take(50)
            .ToList();
        Liste.ItemsSource = bulunan;
        Liste.SelectedIndex = bulunan.Count > 0 ? 0 : -1;
        Acilir.IsOpen = bulunan.Count > 0;
    }

    private void Kutu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Acilir.IsOpen) return;
        switch (e.Key)
        {
            case Key.Down:
                Liste.SelectedIndex = Math.Min(Liste.SelectedIndex + 1, Liste.Items.Count - 1);
                Liste.ScrollIntoView(Liste.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                Liste.SelectedIndex = Math.Max(Liste.SelectedIndex - 1, 0);
                Liste.ScrollIntoView(Liste.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter or Key.Tab:
                if (Liste.SelectedItem is MukellefOzeti m)
                {
                    Sec(m);
                    e.Handled = e.Key == Key.Enter;
                }
                break;
            case Key.Escape:
                Acilir.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void Liste_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: MukellefOzeti m }) Sec(m);
    }

    private void Liste_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Liste.SelectedItem is MukellefOzeti m)
        {
            Sec(m);
            e.Handled = true;
        }
    }

    private void Kutu_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Liste tıklanırken odak popup'a geçer; o durumda kapatma.
        if (e.NewFocus is DependencyObject d && Acilir.Child is { } c && c.IsAncestorOf(d)) return;
        Acilir.IsOpen = false;
    }

    private void Sec(MukellefOzeti m)
    {
        _metinAyarlaniyor = true;
        Kutu.Text = m.Unvan;
        Kutu.CaretIndex = m.Unvan.Length;
        _metinAyarlaniyor = false;
        Secili = m;
        Acilir.IsOpen = false;
        Kutu.Focus();
    }
}
