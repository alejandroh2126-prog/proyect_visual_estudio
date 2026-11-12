namespace Entidades
{
    // Los datos privados nunca se exponen mediante CandidatoPublico.
    public class PerfilCandidato
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Ciudad { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Profesion { get; set; } = "";
        public string Experiencia { get; set; } = "";
        public string TipoDocumento { get; set; } = "CC";
        public string NumeroDocumento { get; set; } = "";
        public string HojaVida { get; set; } = "";
        public string DocumentoFrente { get; set; } = "";
        public string DocumentoReverso { get; set; } = "";
        public bool Visible { get; set; }
    }
    public class CandidatoPublico
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string Ciudad { get; set; } = "";
        public string Profesion { get; set; } = "";
        public string Experiencia { get; set; } = "";
        public bool TieneHojaVida { get; set; }
    }
}
