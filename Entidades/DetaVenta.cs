namespace Entidades
{
    /// <summary>Una linea de la venta: producto, cantidad y precio.</summary>
    public class DetalleVenta
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal { get { return Cantidad * PrecioUnitario; } }
    }
}