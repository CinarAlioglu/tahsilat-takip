using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TahsilatTakip.App.Yardimcilar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Pencereler;

public partial class GorusmePenceresi
{
    private GorusmePenceresi(GorusmeModeli model)
    {
        DataContext = model;
        InitializeComponent();
        model.Kapat = () => DialogResult = true;
        Loaded += (_, _) =>
        {
            if (model.MukellefSabit) NotKutusu.Focus();
            else MukellefKutusu.Focus();
        };
    }

    public static bool Ac(int? mukellefId = null)
    {
        var model = new GorusmeModeli(
            App.Servisler.GetRequiredService<GorusmeServisi>(),
            App.Servisler.GetRequiredService<MukellefServisi>(),
            App.Servisler.GetRequiredService<Oturum>());
        model.Yukle(mukellefId);
        return new GorusmePenceresi(model) { Owner = PencereYardimcisi.Sahip() }.ShowDialog() == true;
    }
}

public partial class GorusmeModeli(GorusmeServisi gorusmeler, MukellefServisi mukellefler, Oturum oturum) : ObservableObject
{
    public Action? Kapat { get; set; }
    public List<MukellefOzeti> Mukellefler { get; private set; } = [];
    public List<Secenek<GorusmeKanali>> Kanallar => Secenekler.Kanallar;

    [ObservableProperty] private bool _mukellefSabit;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BakiyeMetni))] private MukellefOzeti? _seciliMukellef;
    [ObservableProperty] private DateTime? _tarih = DateTime.Today;
    [ObservableProperty] private Secenek<GorusmeKanali> _kanal = Secenekler.Kanallar[0];
    [ObservableProperty] private string? _not;
    [ObservableProperty] private bool _sozVar;
    [ObservableProperty] private DateTime? _sozTarihi;
    [ObservableProperty] private string? _sozTutari;

    public string BakiyeMetni => SeciliMukellef switch
    {
        null => "",
        { Bakiye: > 0 } m => $"Güncel borç: {Sayi.Yaz(m.Bakiye)} ₺" + (m.GecikmeGunu > 0 ? $" · {m.GecikmeGunu} gün gecikmede" : ""),
        _ => "Borcu yok",
    };

    partial void OnSozVarChanged(bool value)
    {
        if (value && SozTarihi is null) SozTarihi = DateTime.Today.AddDays(7);
    }

    public void Yukle(int? mukellefId)
    {
        Mukellefler = mukellefler.Listele(DateOnly.FromDateTime(DateTime.Today));
        OnPropertyChanged(nameof(Mukellefler));
        if (mukellefId is { } id)
        {
            SeciliMukellef = Mukellefler.FirstOrDefault(m => m.Id == id);
            MukellefSabit = SeciliMukellef is not null;
        }
    }

    [RelayCommand]
    private void Kaydet()
    {
        if (SeciliMukellef is null) throw new IslemHatasi("Listeden bir mükellef seçin.");
        if (Tarih is null) throw new IslemHatasi("Görüşme tarihini seçin.");
        DateOnly? sozTarihi = null;
        decimal? sozTutari = null;
        if (SozVar)
        {
            if (SozTarihi is null) throw new IslemHatasi("Ödeme sözü tarihini seçin.");
            sozTarihi = DateOnly.FromDateTime(SozTarihi.Value);
            if (!string.IsNullOrWhiteSpace(SozTutari))
                sozTutari = Sayi.Oku(SozTutari) ?? throw new IslemHatasi("Söz tutarını sayı olarak girin.");
        }
        // Bugün girilen görüşmeye saat de yazılır (aynı gün birden çok görüşmenin sırası için).
        var tarih = Tarih.Value.Date == DateTime.Today ? DateTime.Now : Tarih.Value.Date.AddHours(12);
        gorusmeler.Ekle(SeciliMukellef.Id, tarih, Kanal.Deger, Not ?? "", sozTarihi, sozTutari, oturum.Kullanici?.Id);
        Kapat?.Invoke();
    }
}
