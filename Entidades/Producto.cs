using System;

namespace Entidades
{
    /// <summary>
    /// Producto del inventario. Contiene sus propias reglas (stock, margen).
    /// PILAR: ENCAPSULAMIENTO. El stock solo cambia por AgregarStock/ReducirStock.
    /// </summary>
    public class Producto
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public int StockMinimo { get; set; }
        public int Stock { get; private set; }

        public void EstablecerStock(int cantidad)
        {
            if (cantidad < 0)
                throw new ArgumentException("El stock no puede ser negativo.");
            Stock = cantidad;
        }

        public void AgregarStock(int cantidad)
        {
            if (cantidad <= 0)
                throw new ArgumentException("La cantidad debe ser mayor que cero.");
            Stock += cantidad;
        }

        public void ReducirStock(int cantidad)
        {
            if (cantidad <= 0)
                throw new ArgumentException("La cantidad debe ser mayor que cero.");
            if (cantidad > Stock)
                throw new InvalidOperationException("Stock insuficiente. Disponible: " + Stock);
            Stock -= cantidad;
        }

        public bool HayStock(int cantidad) { return Stock >= cantidad; }
        public bool TieneStockBajo { get { return Stock <= StockMinimo; } }
        public decimal MargenGanancia { get { return PrecioVenta - PrecioCompra; } }

        public decimal PorcentajeMargen
        {
            get
            {
                if (PrecioCompra == 0m) return 0m;
                return (PrecioVenta - PrecioCompra) / PrecioCompra * 100m;
            }
        }

        public override string ToString()
        {
            return Nombre;
        }
    }
}