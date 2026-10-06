using System;
using System.Globalization;

namespace Entidades
{
    /// <summary>Limite de gasto para una categoria en un mes (formato del mes: yyyy-MM).</summary>
    public class Presupuesto
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal MontoLimite { get; set; }
        public string Mes { get; set; } = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}