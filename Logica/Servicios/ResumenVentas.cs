using System.Collections.Generic;

namespace Logica.Servicios
{
    /// <summary>Totales de ventas por periodo y ranking de productos (solo datos).</summary>
    public class ResumenVentas
    {
        public decimal Hoy { get; set; }
        public decimal UltimosSieteDias { get; set; }
        public decimal EsteMes { get; set; }
        public decimal TotalHistorico { get; set; }
        public int CantidadVentas { get; set; }
        public List<KeyValuePair<string, int>> TopProductos { get; set; } = new List<KeyValuePair<string, int>>();
    }
}