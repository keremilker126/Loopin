namespace Loopin.Models
{
    public class Video
    {
        public int Id { get; set; }               // Birincil anahtar
        public string Baslik { get; set; }        // Video başlığı
        public string VideoUrl { get; set; }        // Video dosya yolu / linki
        public string KapakResmiUrl { get; set; } // Kapak resmi URL'i
        public string Aciklama { get; set; }      // Açıklama
        public DateTime YuklenmeTarihi { get; set; } = DateTime.Now;
        public int IzlenmeSayisi { get; set; } = 0;
        public int LikeSayisi { get; set; } = 0;
        public List<Yorum>? VideoYorumlar { get; set; }
        public List<Begenme>? VideoBegenmeler { get; set; }
        public int AboneSayisi { get; set; } = 0; // Abone sayısı


        // Yabancı anahtar
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }
    }
}
