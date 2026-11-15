using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    /// <summary>
    /// Una venta vive en DOS tablas: sg_venta (cabecera) y sg_detalle_venta (productos vendidos).
    /// Al guardar se usa una TRANSACCION: o se guarda todo (cabecera + detalles) o no se guarda nada.
    /// </summary>
    public class VentaDaoOracle : DaoOracle<Venta>, IVentaDao
    {
        protected override string Tabla { get { return "sg_venta"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "emprendimiento_id", "fecha", "observacion" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, emprendimiento_id, fecha, observacion FROM sg_venta"; }
        }

        protected override Venta Mapear(DbDataReader r)
        {
            return new Venta
            {
                Id = ConexionOracle.Entero(r, "id"),
                EmprendimientoId = ConexionOracle.Entero(r, "emprendimiento_id"),
                Fecha = ConexionOracle.Fecha(r, "fecha"),
                Observacion = ConexionOracle.Texto(r, "observacion")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Venta v)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, v.EmprendimientoId);
            ConexionOracle.Param(cmd, "fecha", DbType.DateTime, v.Fecha);
            ConexionOracle.Param(cmd, "observacion", DbType.String, v.Observacion);
        }

        protected override int ObtenerId(Venta v) { return v.Id; }
        protected override void AsignarId(Venta v, int id) { v.Id = id; }

        public override List<Venta> Listar()
        {
            return ConexionOracle.Ejecutar(con =>
            {
                List<Venta> ventas = Consultar(con, SqlSeleccion + " ORDER BY id", null);
                CargarDetalles(con, ventas, null);
                return ventas;
            });
        }

        public override Venta? ObtenerPorId(int id)
        {
            return ConexionOracle.Ejecutar(con =>
            {
                List<Venta> ventas = Consultar(con, SqlSeleccion + " WHERE id = :p_id", cmd =>
                    ConexionOracle.Param(cmd, "id", DbType.Int32, id));
                CargarDetalles(con, ventas, id);
                return ventas.Count > 0 ? ventas[0] : null;
            });
        }

        public override Venta Insertar(Venta venta)
        {
            ConexionOracle.Ejecutar(con =>
            {
                using (DbTransaction transaccion = con.BeginTransaction())
                {
                    InsertarCon(con, venta, transaccion);
                    InsertarDetalles(con, venta, transaccion);
                    transaccion.Commit();
                }
            });
            return venta;
        }

        public override void Actualizar(Venta venta)
        {
            ConexionOracle.Ejecutar(con =>
            {
                using (DbTransaction transaccion = con.BeginTransaction())
                {
                    ActualizarCon(con, venta, transaccion);
                    using (DbCommand cmd = ConexionOracle.Comando(con, "DELETE FROM sg_detalle_venta WHERE venta_id = :p_venta"))
                    {
                        cmd.Transaction = transaccion;
                        ConexionOracle.Param(cmd, "venta", DbType.Int32, venta.Id);
                        cmd.ExecuteNonQuery();
                    }
                    InsertarDetalles(con, venta, transaccion);
                    transaccion.Commit();
                }
            });
        }

        // Los detalles se borran solos (ON DELETE CASCADE) al eliminar la venta.
        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }

        private void InsertarDetalles(DbConnection con, Venta venta, DbTransaction transaccion)
        {
            foreach (DetalleVenta d in venta.Detalles)
            {
                using (DbCommand cmd = ConexionOracle.Comando(con,
                    "INSERT INTO sg_detalle_venta (venta_id, producto_id, producto_nombre, cantidad, precio_unitario) " +
                    "VALUES (:p_venta, :p_producto, :p_nombre, :p_cantidad, :p_precio)"))
                {
                    cmd.Transaction = transaccion;
                    ConexionOracle.Param(cmd, "venta", DbType.Int32, venta.Id);
                    ConexionOracle.Param(cmd, "producto", DbType.Int32, d.ProductoId);
                    ConexionOracle.Param(cmd, "nombre", DbType.String, d.ProductoNombre);
                    ConexionOracle.Param(cmd, "cantidad", DbType.Int32, d.Cantidad);
                    ConexionOracle.Param(cmd, "precio", DbType.Decimal, d.PrecioUnitario);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>Lee los detalles de todas las ventas (o de una sola) y los reparte en su venta.</summary>
        private void CargarDetalles(DbConnection con, List<Venta> ventas, int? ventaId)
        {
            if (ventas.Count == 0) return;

            Dictionary<int, Venta> porId = new Dictionary<int, Venta>();
            foreach (Venta v in ventas) porId[v.Id] = v;

            string sql = "SELECT venta_id, producto_id, producto_nombre, cantidad, precio_unitario FROM sg_detalle_venta";
            if (ventaId != null) sql += " WHERE venta_id = :p_venta";
            sql += " ORDER BY venta_id, id";

            using (DbCommand cmd = ConexionOracle.Comando(con, sql))
            {
                if (ventaId != null) ConexionOracle.Param(cmd, "venta", DbType.Int32, ventaId.Value);
                using (DbDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        int idVenta = ConexionOracle.Entero(lector, "venta_id");
                        Venta? venta;
                        if (!porId.TryGetValue(idVenta, out venta)) continue;
                        venta.Detalles.Add(new DetalleVenta
                        {
                            ProductoId = ConexionOracle.Entero(lector, "producto_id"),
                            ProductoNombre = ConexionOracle.Texto(lector, "producto_nombre"),
                            Cantidad = ConexionOracle.Entero(lector, "cantidad"),
                            PrecioUnitario = ConexionOracle.Decimal(lector, "precio_unitario")
                        });
                    }
                }
            }
        }
    }
}
