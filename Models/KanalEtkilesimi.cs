namespace Loopin.Models
{
    public class KanalEtkilesimi
    {
        public const string Goruntulenme = "Goruntulenme";
        public const string Begeni = "Begeni";
        public const string DahaSonraIzle = "DahaSonraIzle";

        public int Id { get; set; }
        public string Tur { get; set; } = "";
        public DateTime Tarih { get; set; } = DateTime.Now;

        public int VideoId { get; set; }
        public Video Video { get; set; } = null!;
    }
}
