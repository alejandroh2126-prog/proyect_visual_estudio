using System;

namespace Entidades
{
    /// <summary>Registro del historial financiero (ventas, compras, gastos, ingresos).</summary>
    public class Movimiento
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public TipoMovimiento Tipo { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Referencia { get; set; } = string.Empty;

        public bool EsIngreso
        {
            get { return Tipo == TipoMovimiento.Venta || Tipo == TipoMovimiento.Ingreso; }
        }



        public bool EsGasto
        {
            get { return Tipo == TipoMovimiento.Compra || Tipo == TipoMovimiento.Gasto; }
        }
    }
}