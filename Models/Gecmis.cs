using Loopin.Models;

public class Gecmis
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public int VideoId { get; set; }
    public DateTime IzlenmeTarihi { get; set; }

    public Kullanici Kullanici { get; set; }
    public Video Video { get; set; }
}
