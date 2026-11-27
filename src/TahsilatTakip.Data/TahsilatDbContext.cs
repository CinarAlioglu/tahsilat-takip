using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data;

public class TahsilatDbContext(DbContextOptions<TahsilatDbContext> options, Oturum? oturum = null) : DbContext(options)
{
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Mukellef> Mukellefler => Set<Mukellef>();
    public DbSet<HizmetTuru> HizmetTurleri => Set<HizmetTuru>();
    public DbSet<UcretPlani> UcretPlanlari => Set<UcretPlani>();
    public DbSet<Tahakkuk> Tahakkuklar => Set<Tahakkuk>();
    public DbSet<Tahsilat> Tahsilatlar => Set<Tahsilat>();
    public DbSet<GorusmeNotu> GorusmeNotlari => Set<GorusmeNotu>();
    public DbSet<OdemeGrubu> OdemeGruplari => Set<OdemeGrubu>();
    public DbSet<IslemGunlugu> IslemGunlukleri => Set<IslemGunlugu>();
    public DbSet<Ayar> Ayarlar => Set<Ayar>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Kullanici>(e =>
        {
            e.ToTable("Kullanicilar");
            e.Property(x => x.KullaniciAdi).HasMaxLength(50).UseCollation("NOCASE");
            e.HasIndex(x => x.KullaniciAdi).IsUnique();
            e.Property(x => x.AdSoyad).HasMaxLength(100);
        });

        b.Entity<Mukellef>(e =>
        {
            e.ToTable("Mukellefler");
            e.Property(x => x.Unvan).HasMaxLength(250);
            e.HasIndex(x => x.Unvan);
            e.Property(x => x.VknTckn).HasMaxLength(11);
            e.HasOne(x => x.OdemeGrubu).WithMany(g => g.Mukellefler)
                .HasForeignKey(x => x.OdemeGrubuId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<HizmetTuru>(e =>
        {
            e.ToTable("HizmetTurleri");
            e.Property(x => x.Ad).HasMaxLength(100);
            e.HasData(
                new HizmetTuru { Id = HizmetTuru.AylikMuhasebeUcretiId, Ad = "Muhasebe ücreti", Sistem = true },
                new HizmetTuru { Id = HizmetTuru.DefterTasdikiId, Ad = "Defter tasdiki", Sistem = true },
                new HizmetTuru { Id = HizmetTuru.DevirId, Ad = "Devir (açılış bakiyesi)", Sistem = true },
                new HizmetTuru { Id = HizmetTuru.DigerId, Ad = "Diğer hizmet", Sistem = true });
        });

        b.Entity<UcretPlani>(e =>
        {
            e.ToTable("UcretPlanlari");
            e.HasOne(x => x.Mukellef).WithMany(m => m.UcretPlanlari)
                .HasForeignKey(x => x.MukellefId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HizmetTuru).WithMany().HasForeignKey(x => x.HizmetTuruId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MukellefId, x.HizmetTuruId, x.GecerlilikBaslangic }).IsUnique();
        });

        b.Entity<Tahakkuk>(e =>
        {
            e.ToTable("Tahakkuklar");
            e.Property(x => x.Aciklama).HasMaxLength(300);
            e.HasOne(x => x.Mukellef).WithMany(m => m.Tahakkuklar)
                .HasForeignKey(x => x.MukellefId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HizmetTuru).WithMany().HasForeignKey(x => x.HizmetTuruId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MukellefId, x.Tarih });
            // Aynı dönem için ikinci kez otomatik borç yazılmasını veritabanı düzeyinde de engeller.
            e.HasIndex(x => new { x.MukellefId, x.HizmetTuruId, x.DonemBaslangic })
                .IsUnique().HasFilter("Kaynak = 0").HasDatabaseName("IX_Tahakkuklar_OtomatikDonem");
        });

        b.Entity<Tahsilat>(e =>
        {
            e.ToTable("Tahsilatlar");
            e.HasOne(x => x.Mukellef).WithMany(m => m.Tahsilatlar)
                .HasForeignKey(x => x.MukellefId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MukellefId, x.Tarih });
            e.HasIndex(x => x.GrupIslemNo);
        });

        b.Entity<GorusmeNotu>(e =>
        {
            e.ToTable("GorusmeNotlari");
            e.HasOne(x => x.Mukellef).WithMany(m => m.GorusmeNotlari)
                .HasForeignKey(x => x.MukellefId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SozDurumu, x.SozTarihi });
        });

        b.Entity<OdemeGrubu>(e =>
        {
            e.ToTable("OdemeGruplari");
            e.Property(x => x.Ad).HasMaxLength(150);
        });

        b.Entity<IslemGunlugu>(e =>
        {
            e.ToTable("IslemGunlugu");
            e.HasIndex(x => x.Tarih);
            e.HasIndex(x => new { x.Tablo, x.KayitId });
        });

        b.Entity<Ayar>(e =>
        {
            e.ToTable("Ayarlar");
            e.HasKey(x => x.Anahtar);
        });
    }

    // ---- İşlem günlüğü ----------------------------------------------------------------

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var bekleyen = GunlukKayitlariniHazirla();
        var sonuc = base.SaveChanges(acceptAllChangesOnSuccess);
        if (GunlukleriEkle(bekleyen)) base.SaveChanges(acceptAllChangesOnSuccess);
        return sonuc;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        var bekleyen = GunlukKayitlariniHazirla();
        var sonuc = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        if (GunlukleriEkle(bekleyen)) await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        return sonuc;
    }

    private sealed record BekleyenGunluk(EntityEntry Entry, string Islem, string? Detay);

    private static readonly HashSet<string> GizliAlanlar = ["SifreHash"];

    private List<BekleyenGunluk> GunlukKayitlariniHazirla()
    {
        ChangeTracker.DetectChanges();
        var liste = new List<BekleyenGunluk>();
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.Entity is IslemGunlugu or Ayar) continue;
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            Dictionary<string, object?> alanlar;
            string islem;
            switch (e.State)
            {
                case EntityState.Added:
                    islem = "Eklendi";
                    alanlar = e.Properties.Where(p => !p.Metadata.IsPrimaryKey())
                        .ToDictionary(p => p.Metadata.Name, p => Gizle(p.Metadata.Name, p.CurrentValue));
                    break;
                case EntityState.Modified:
                    islem = e.Entity is IIptalEdilebilir { IptalEdildi: true }
                            && e.Property(nameof(IIptalEdilebilir.IptalEdildi)).IsModified
                        ? "İptal edildi"
                        : "Değiştirildi";
                    alanlar = e.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .ToDictionary(p => p.Metadata.Name,
                            p => (object?)new { Eski = Gizle(p.Metadata.Name, p.OriginalValue), Yeni = Gizle(p.Metadata.Name, p.CurrentValue) });
                    if (alanlar.Count == 0) continue;
                    break;
                default:
                    islem = "Silindi";
                    alanlar = e.Properties.ToDictionary(p => p.Metadata.Name, p => Gizle(p.Metadata.Name, p.OriginalValue));
                    break;
            }
            liste.Add(new BekleyenGunluk(e, islem, JsonSerializer.Serialize(alanlar)));
        }
        return liste;
    }

    private static object? Gizle(string alan, object? deger) => GizliAlanlar.Contains(alan) ? "***" : deger;

    private bool GunlukleriEkle(List<BekleyenGunluk> bekleyen)
    {
        if (bekleyen.Count == 0) return false;
        var simdi = DateTime.Now;
        foreach (var g in bekleyen)
        {
            var anahtar = g.Entry.Metadata.FindPrimaryKey()!.Properties
                .Select(p => g.Entry.Property(p.Name).CurrentValue?.ToString() ?? g.Entry.Property(p.Name).OriginalValue?.ToString());
            IslemGunlukleri.Add(new IslemGunlugu
            {
                Tarih = simdi,
                KullaniciId = oturum?.Kullanici?.Id,
                Tablo = g.Entry.Metadata.GetTableName() ?? g.Entry.Metadata.ClrType.Name,
                KayitId = string.Join(",", anahtar),
                Islem = g.Islem,
                Detay = g.Detay,
            });
        }
        return true;
    }
}
