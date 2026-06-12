namespace Loopin.Models
{
    public class MobilBildirim
    {
        public int Id { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; } = null!;
        public int? VideoId { get; set; }
        public Video? Video { get; set; }
        public int KanalKullaniciId { get; set; }
        public Kullanici KanalKullanici { get; set; } = null!;
        public string Tip { get; set; } = "";
        public string Baslik { get; set; } = "";
        public string Mesaj { get; set; } = "";
        public bool Okundu { get; set; }
        public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;
        public DateTime? OkunmaTarihi { get; set; }
    }
}
