using System;

namespace Entidades
{
    /// <summary>
    /// PILAR: ABSTRACCION. Clase base que no se puede instanciar directamente.
    /// PILAR: ENCAPSULAMIENTO. Los datos se exponen por propiedades con validacion.
    /// </summary>
    public abstract class Persona
    {
        private string _nombre = string.Empty;
        private string _apellido = string.Empty;
        private string _cedula = string.Empty;

        protected Persona(string nombre, string apellido, string cedula)
        {
            Nombre = nombre;
            Apellido = apellido;
            Cedula = cedula;
        }

        public string Nombre
        {
            get { return _nombre; }
            set { _nombre = (value ?? string.Empty).Trim(); }
        }

        public string Apellido
        {
            get { return _apellido; }
            set { _apellido = (value ?? string.Empty).Trim(); }
        }

        public string Cedula
        {
            get { return _cedula; }
            set { _cedula = (value ?? string.Empty).Trim(); }
        }

        public string NombreCompleto
        {
            get { return Nombre + " " + Apellido; }
        }

        /// <summary>PILAR: POLIMORFISMO. Cada clase hija responde distinto.</summary>
        public abstract string TipoPersona { get; }

        public override string ToString()
        {
            return TipoPersona + ": " + NombreCompleto + " | CC: " + Cedula;
        }
    }
}