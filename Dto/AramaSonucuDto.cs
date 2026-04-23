public class AramaSonucDto
{
    public List<VideoDto> Videolar { get; set; }
    public List<KullaniciDto> Kullanicilar { get; set; }
    public List<int> Abonelikler { get; set; } // ViewBag.Abonelikler için
}