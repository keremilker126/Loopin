using Loopin.Models;

public class Abonelik
{
    public int Id { get; set; }

    public int AboneOlanId { get; set; }   // abone olan kullanıcı
    public Kullanici AboneOlan { get; set; }

    public int AboneOlunanId { get; set; } // abone olunan kullanıcı
    public Kullanici AboneOlunan { get; set; }

    public DateTime Tarih { get; set; } = DateTime.Now;
}
