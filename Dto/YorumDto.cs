public class YorumDto
{
    public int Id { get; set; }
    public string Icerik { get; set; }
    public DateTime Tarih { get; set; }
    public int KullaniciId { get; set; }
    public string KullaniciAdi { get; set; } // Izle.cshtml için
    public int VideoId { get; set; }
}