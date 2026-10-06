using System;

namespace Entidades
{
    /// <summary>
    /// PILAR: HERENCIA. Trabajador hereda de Persona y agrega lo propio del empleo.
    /// Los calculos salariales se delegan al Cargo (cada clase hace una sola cosa).
    /// </summary>
    public class Trabajador : Persona
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public Cargo Cargo { get; set; }
        public DateTime FechaIngreso { get; set; }
        public string Estado { get; set; }

        public Trabajador(int id, string nombre, string apellido, string cedula,
                          Cargo cargo, DateTime fechaIngreso, int emprendimientoId)
            : base(nombre, apellido, cedula)
        {
            Id = id;
            Cargo = cargo;
            FechaIngreso = fechaIngreso;
            EmprendimientoId = emprendimientoId;
            Estado = Estados.Activo;
        }

        /// <summary>PILAR: POLIMORFISMO. Implementacion propia del miembro abstracto.</summary>
        public override string TipoPersona
        {
            get { return "Trabajador"; }
        }

        public bool EstaActivo
        {
            get { return Estado == Estados.Activo; }
        }

        public decimal SalarioMensual { get { return Cargo.SalarioMensual; } }
        public decimal SalarioQuincenal { get { return Cargo.SalarioQuincenal; } }
        public decimal TotalDeducciones { get { return Cargo.TotalDeducciones; } }
        public decimal SalarioNeto { get { return Cargo.SalarioNeto; } }
    }
}