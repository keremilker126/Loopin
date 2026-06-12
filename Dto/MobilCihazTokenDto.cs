namespace Loopin.Models
{
    public class MobilCihazTokenDto
    {
        public int KullaniciId { get; set; }
        public string Token { get; set; } = "";
        public string Platform { get; set; } = "";
        public string? CihazId { get; set; }
    }
}
