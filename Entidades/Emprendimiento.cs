using System;

namespace Entidades
{
    /// <summary>Negocio o emprendimiento que pertenece a un usuario.</summary>
    public class Emprendimiento
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public string Estado { get; set; } = Estados.Activo;

        public override string ToString()
        {
            return Nombre;
        }
    }
}