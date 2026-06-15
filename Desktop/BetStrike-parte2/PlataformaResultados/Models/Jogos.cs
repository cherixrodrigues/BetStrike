namespace PlataformaResultados.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public DateTime Data { get; set; }
        public TimeSpan Hora { get; set; }
        public string EquipaCasa { get; set; }
        public string EquipaFora { get; set; }
        public string Competicao { get; set; }
        public int Estado { get; set; } = 1;
    }

    public class AtualizacaoJogo
    {
        public string Codigo { get; set; }
        public int Estado { get; set; }
        public int? GolosCasa { get; set; }
        public int? GolosFora { get; set; }
    }
}