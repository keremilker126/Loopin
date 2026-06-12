using Microsoft.EntityFrameworkCore;
using Loopin.Models;

namespace Loopin.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Kullanici> Kullanicilar { get; set; }
        public DbSet<Video> Videolar { get; set; }
        public DbSet<Yorum> Yorumlar { get; set; }
        public DbSet<Begenme> Begenmeler { get; set; }
        public DbSet<Abonelik> Abonelikler { get; set; }
        public DbSet<DahaSonraIzle> DahaSonraIzleListesi { get; set; }
        public DbSet<Gecmis> GecmisListesi { get; set; }
        public DbSet<KanalEtkilesimi> KanalEtkilesimleri { get; set; }
        public DbSet<MobilCihazToken> MobilCihazTokenlari { get; set; }
        public DbSet<MobilBildirim> MobilBildirimleri { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<KanalEtkilesimi>()
                .HasIndex(e => new { e.VideoId, e.Tarih, e.Tur });

            modelBuilder.Entity<MobilCihazToken>()
                .HasIndex(t => t.Token)
                .IsUnique();

            modelBuilder.Entity<MobilCihazToken>()
                .HasIndex(t => new { t.KullaniciId, t.Aktif });

            modelBuilder.Entity<MobilBildirim>()
                .HasIndex(b => new { b.KullaniciId, b.Okundu, b.OlusturulmaTarihi });

            modelBuilder.Entity<MobilBildirim>()
                .HasOne(b => b.Video)
                .WithMany()
                .HasForeignKey(b => b.VideoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MobilBildirim>()
                .HasOne(b => b.KanalKullanici)
                .WithMany()
                .HasForeignKey(b => b.KanalKullaniciId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
