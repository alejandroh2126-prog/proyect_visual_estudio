using System;
using System.Collections.Generic;

namespace Entidades
{
    /// <summary>Venta con sus detalles. Sabe calcular su total.</summary>
    public class Venta
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Observacion { get; set; } = string.Empty;
        public List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();

        public decimal Total
        {
            get
            {
                decimal total = 0m;
                foreach (DetalleVenta d in Detalles) total += d.Subtotal;
                return total;
            }
        }

        public int TotalProductos
        {
            get
            {
                int total = 0;
                foreach (DetalleVenta d in Detalles) total += d.Cantidad;
                return total;
            }
        }

        /// <summary>Si el producto ya esta en la venta, suma la cantidad.</summary>
        public void AgregarDetalle(DetalleVenta nuevo)
        {
            foreach (DetalleVenta d in Detalles)
            {
                if (d.ProductoId == nuevo.ProductoId)
                {
                    d.Cantidad += nuevo.Cantidad;
                    return;
                }
            }
            Detalles.Add(nuevo);
        }
    }
}