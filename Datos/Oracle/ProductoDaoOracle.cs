using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    public class ProductoDaoOracle : DaoOracle<Producto>, IProductoDao
    {
        protected override string Tabla { get { return "sg_producto"; } }
        protected override string[] ColumnasEscritura
        {
            get
            {
                return new[] { "emprendimiento_id", "nombre", "categoria", "precio_compra", "precio_venta", "stock", "stock_minimo" };
            }
        }
        protected override string SqlSeleccion
        {
            get
            {
                return "SELECT id, emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo FROM sg_producto";
            }
        }

        protected override Producto Mapear(DbDataReader r)
        {
            Producto p = new Producto
            {
                Id = ConexionOracle.Entero(r, "id"),
                EmprendimientoId = ConexionOracle.Entero(r, "emprendimiento_id"),
                Nombre = ConexionOracle.Texto(r, "nombre"),
                Categoria = ConexionOracle.Texto(r, "categoria"),
                PrecioCompra = ConexionOracle.Decimal(r, "precio_compra"),
                PrecioVenta = ConexionOracle.Decimal(r, "precio_venta"),
                StockMinimo = ConexionOracle.Entero(r, "stock_minimo")
            };
            p.EstablecerStock(ConexionOracle.Entero(r, "stock"));
            return p;
        }

        protected override void AsignarParametros(DbCommand cmd, Producto p)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, p.EmprendimientoId);
            ConexionOracle.Param(cmd, "nombre", DbType.String, p.Nombre);
            ConexionOracle.Param(cmd, "categoria", DbType.String, p.Categoria);
            ConexionOracle.Param(cmd, "precio_compra", DbType.Decimal, p.PrecioCompra);
            ConexionOracle.Param(cmd, "precio_venta", DbType.Decimal, p.PrecioVenta);
            ConexionOracle.Param(cmd, "stock", DbType.Int32, p.Stock);
            ConexionOracle.Param(cmd, "stock_minimo", DbType.Int32, p.StockMinimo);
        }

        protected override int ObtenerId(Producto p) { return p.Id; }
        protected override void AsignarId(Producto p, int id) { p.Id = id; }

        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }
    }
}
