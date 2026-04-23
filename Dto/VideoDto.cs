public class VideoDto
{
    public int Id { get; set; }
    public string Baslik { get; set; }
    public string VideoUrl { get; set; }
    public string KapakResmiUrl { get; set; }
    public string Aciklama { get; set; }
    public DateTime YuklenmeTarihi { get; set; }
    public int IzlenmeSayisi { get; set; }
    public int LikeSayisi { get; set; }
    public int KullaniciId { get; set; }
    public string KullaniciAdi { get; set; }
    public int AboneSayisi { get; set; }

    // Flutter tarafında kafa karışıklığı olmaması için doğrudan bunları kullanalım

}