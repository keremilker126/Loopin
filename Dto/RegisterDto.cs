using System.ComponentModel.DataAnnotations;

public class RegisterDto
{
    [Required]
    [StringLength(40, MinimumLength = 3)]
    public string KullaniciAdi { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(120)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(64, MinimumLength = 6)]
    public string Sifre { get; set; } = "";
}
