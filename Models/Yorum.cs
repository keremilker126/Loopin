namespace Loopin.Models
{
    public class Yorum
    {
        public int Id { get; set; }
        public string Icerik { get; set; }       // Yorum metni
        public DateTime Tarih { get; set; } = DateTime.Now;

        // İlişkiler
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        public int VideoId { get; set; }
        public Video Video { get; set; }
    }
}
