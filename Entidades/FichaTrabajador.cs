namespace Entidades
{
    public class FichaTrabajador
    {
        public int Id { get; set; }
        public string ClaveTrabajador { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Correo { get; set; } = "";
        public string Ciudad { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string Nacionalidad { get; set; } = "";
        public string Estudios { get; set; } = "";
        public string TipoContrato { get; set; } = "";
        public string Foto { get; set; } = "";
        public string DocumentoFrente { get; set; } = "";
        public string DocumentoReverso { get; set; } = "";
        public string HojaVida { get; set; } = "";
    }
    public enum AdjuntoTrabajador { Foto, DocumentoFrente, DocumentoReverso, HojaVida }
}
