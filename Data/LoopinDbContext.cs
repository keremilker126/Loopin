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
        
    }
}
