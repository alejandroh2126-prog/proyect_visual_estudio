using System;

namespace Entidades
{
    /// <summary>Usuario que inicia sesion. Nunca se guarda la clave en texto plano.</summary>
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ClaveHash { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; } = DateTime.Today;
    }
}