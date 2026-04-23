

// Temel kullanıcı bilgileri (Giriş sonrası veya profil görünümü için)
public class KullaniciDto
{
    public int Id { get; set; }
    public string? KullaniciAdi { get; set; }
    public string Email { get; set; }
    public bool EmailOnayli { get; set; }
    public int AboneSayisi { get; set; }
}
