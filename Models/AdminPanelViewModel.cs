namespace Loopin.Models
{
    public class AdminPanelViewModel
    {
        public List<Video> Videolar { get; set; } = new();
        public List<Kullanici> Kullanicilar { get; set; } = new();
        public List<string> EngellenenEpostalar { get; set; } = new();
        public List<ChannelStatsViewModel> KanalIstatistikleri { get; set; } = new();
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
    }

    public class ChannelStatsViewModel
    {
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } = "";
        public string Email { get; set; } = "";
        public int VideoSayisi { get; set; }
        public int ToplamLike { get; set; }
        public int ToplamGoruntulenme { get; set; }
        public int DahaSonraIzleSayisi { get; set; }
        public List<ChannelDailyStatsViewModel> GunlukIstatistikler { get; set; } = new();
    }

    public class ChannelDailyStatsViewModel
    {
        public DateTime Tarih { get; set; }
        public int GoruntulenmeSayisi { get; set; }
        public int BegeniSayisi { get; set; }
        public int DahaSonraIzleSayisi { get; set; }
    }
}
