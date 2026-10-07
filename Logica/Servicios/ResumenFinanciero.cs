namespace Logica.Servicios
{
    /// <summary>Totales financieros de un emprendimiento (solo datos).</summary>
    public class ResumenFinanciero
    {
        public decimal TotalIngresos { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal Balance { get { return TotalIngresos - TotalGastos; } }
    }
}