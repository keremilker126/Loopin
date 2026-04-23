using System;
using System.Collections.Generic;

namespace Loopin.Models
{
    public class Kullanici
    {
        public int Id { get; set; }                  // Birincil anahtar
        public string? KullaniciAdi { get; set; }    // Kullanıcı adı
        public string Email { get; set; }            // E-posta
        public string Sifre { get; set; }            // Şifre (hashlenmeli!)

        // ✅ E-posta doğrulama
        public bool EmailOnayli { get; set; }        // Onay durumu
        public string? EmailOnayToken { get; set; }  // Token (GUID)
        public DateTime? EmailOnayExpire { get; set; } // Token geçerlilik süresi

        // ✅ Şifre sıfırlama
        public string? ResetCode { get; set; }       // Doğrulama kodu
        public DateTime? ResetCodeExpire { get; set; } // Kodun geçerlilik süresi

        // İlişkili videolar
        public List<Video>? Videolar { get; set; }
    }
}
