using Loopin.Models;

public class DahaSonraIzle
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public int VideoId { get; set; }
    public DateTime EklenmeTarihi { get; set; }

    public Kullanici Kullanici { get; set; }
    public Video Video { get; set; }
}
