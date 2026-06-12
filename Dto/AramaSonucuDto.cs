public class AramaSonucDto
{
    public List<VideoDto> Videolar { get; set; } = new();
    public List<KullaniciDto> Kullanicilar { get; set; } = new();
    public List<int> Abonelikler { get; set; } = new(); // ViewBag.Abonelikler için
}
