using System.ComponentModel.DataAnnotations;

public class LoginDto
{
    [Required]
    [EmailAddress]
    [StringLength(120)]
    public string Email { get; set; } = "";

    [Required]
    public string Sifre { get; set; } = "";
}
