using System;

namespace Entidades
{
    /// <summary>
    /// Cargo laboral de un emprendimiento. Sabe calcular su propio salario
    /// (quincenal, deducciones y neto). Una sola mision: describir un cargo.
    /// </summary>
    public class Cargo
    {
        public const decimal PorcentajeSalud = 0.04m;
        public const decimal PorcentajePension = 0.04m;
        public const decimal TotalPorcentajeDeducciones = PorcentajeSalud + PorcentajePension;

        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public string Descripcion { get; set; } = string.Empty;

        public decimal SalarioMensual { get { return SalarioBase; } }
        public decimal SalarioQuincenal { get { return SalarioBase / 2m; } }
        public decimal DeduccionSalud { get { return SalarioBase * PorcentajeSalud; } }
        public decimal DeduccionPension { get { return SalarioBase * PorcentajePension; } }
        public decimal TotalDeducciones { get { return SalarioBase * TotalPorcentajeDeducciones; } }
        public decimal SalarioNeto { get { return SalarioBase - TotalDeducciones; } }

        public override string ToString()
        {
            return Nombre;
        }
    }
}