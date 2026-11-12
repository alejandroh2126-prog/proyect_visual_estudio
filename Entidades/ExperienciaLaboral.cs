using System;
namespace Entidades
{
    public class ExperienciaLaboral
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Empresa { get; set; } = "";
        public string Cargo { get; set; } = "";
        public DateTime Inicio { get; set; } = DateTime.Today;
        public DateTime Fin { get; set; } = DateTime.Today;
        public string Motivo { get; set; } = "";
    }
    public static class MotivosRetiro
    {
        public static readonly string[] Valores = { "Renuncia voluntaria", "Finalización del contrato", "Mutuo acuerdo", "Terminación por decisión del empleador", "Otro / no especificado" };
        public static bool EsValido(string motivo) => Array.IndexOf(Valores, motivo) >= 0;
    }
}
