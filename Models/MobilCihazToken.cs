namespace Loopin.Models
{
    public class MobilCihazToken
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public string Token { get; set; } = "";
        public string Platform { get; set; } = "";
        public string? CihazId { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public DateTime? GuncellenmeTarihi { get; set; }
    }
}
