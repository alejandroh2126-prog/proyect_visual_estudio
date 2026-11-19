using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using Entidades;
using Oracle.ManagedDataAccess.Client;
using static Datos.Oracle.ConexionOracle;

namespace Datos.Oracle
{
    public class ProductoDaoOracle : IProductoDao
    {
        private const string Sql =
            "SELECT producto_id, emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo, " +
            "stock_bajo, margen_valor, margen_porcentaje, valor_inventario FROM vw_sg_margen_producto";

        private static Producto Mapear(OracleDataReader r)
        {
            return new Producto
            {
                Id = Int(r, "producto_id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                Nombre = Txt(r, "nombre"),
                Categoria = Txt(r, "categoria"),
                PrecioCompra = Dec(r, "precio_compra"),
                PrecioVenta = Dec(r, "precio_venta"),
                Stock = Int(r, "stock"),
                StockMinimo = Int(r, "stock_minimo"),
                StockBajo = SiNo(r, "stock_bajo"),
                MargenValor = Dec(r, "margen_valor"),
                MargenPorcentaje = Dec(r, "margen_porcentaje"),
                ValorInventario = Dec(r, "valor_inventario")
            };
        }

        public List<Producto> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp ORDER BY nombre", Mapear, c => Entero(c, "emp", emprendimientoId));
        }

        public Producto? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE producto_id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Producto Insertar(Producto p)
        {
            p.Id = InsertarConId(
                "INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo) " +
                "VALUES (:p_emp, :p_nombre, :p_categoria, :p_compra, :p_venta, :p_stock, :p_minimo)",
                c =>
                {
                    Entero(c, "emp", p.EmprendimientoId);
                    Texto(c, "nombre", p.Nombre);
                    Texto(c, "categoria", p.Categoria);
                    Numero(c, "compra", p.PrecioCompra);
                    Numero(c, "venta", p.PrecioVenta);
                    Entero(c, "stock", p.Stock);
                    Entero(c, "minimo", p.StockMinimo);
                });
            return p;
        }

        public void Actualizar(Producto p)
        {
            // Si cambia un precio, el disparador trg_sg_producto_precio_aud lo deja en la auditoria.
            EjecutarSql("UPDATE sg_producto SET nombre = :p_nombre, categoria = :p_categoria, precio_compra = :p_compra, " +
                        "precio_venta = :p_venta, stock_minimo = :p_minimo WHERE id = :p_id",
                c =>
                {
                    Texto(c, "nombre", p.Nombre);
                    Texto(c, "categoria", p.Categoria);
                    Numero(c, "compra", p.PrecioCompra);
                    Numero(c, "venta", p.PrecioVenta);
                    Entero(c, "minimo", p.StockMinimo);
                    Entero(c, "id", p.Id);
                });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_producto WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }

        public List<string> CategoriasUsadas(int emprendimientoId)
        {
            return Lista("SELECT DISTINCT categoria FROM sg_producto WHERE emprendimiento_id = :p_emp AND categoria IS NOT NULL ORDER BY categoria",
                         r => Txt(r, "categoria"), c => Entero(c, "emp", emprendimientoId));
        }

        public void MoverStock(int productoId, int cantidad, bool esEntrada)
        {
            EjecutarSql("BEGIN sp_sg_mover_stock(:p_producto, :p_cantidad, :p_tipo); END;",
                c =>
                {
                    Entero(c, "producto", productoId);
                    Entero(c, "cantidad", cantidad);
                    Texto(c, "tipo", esEntrada ? "ENTRADA" : "SALIDA");
                });
        }

        public int AjustarPrecios(int emprendimientoId, decimal porcentaje)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_ajustar_precio_venta(:p_emp, :p_porcentaje, :p_afectados); END;"))
                {
                    Entero(cmd, "emp", emprendimientoId);
                    Numero(cmd, "porcentaje", porcentaje);
                    OracleParameter afectados = Param(cmd, "afectados", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(afectados);
                }
            });
        }

        public List<ProductoStockBajo> StockBajo(int emprendimientoId)
        {
            return ListaCursor("BEGIN sp_sg_productos_stock_bajo(:p_emp, :p_resultado); END;",
                r => new ProductoStockBajo
                {
                    ProductoId = Int(r, "producto_id"),
                    Nombre = Txt(r, "nombre"),
                    Categoria = Txt(r, "categoria"),
                    Stock = Int(r, "stock"),
                    StockMinimo = Int(r, "stock_minimo"),
                    Faltante = Int(r, "faltante")
                },
                c => Entero(c, "emp", emprendimientoId));
        }
    }

    public class VentaDaoOracle : IVentaDao
    {
        public List<Venta> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista("SELECT venta_id, emprendimiento_id, fecha, observacion, productos, total FROM vw_sg_ventas_resumen " +
                         "WHERE emprendimiento_id = :p_emp ORDER BY fecha DESC, venta_id DESC",
                r => new Venta
                {
                    Id = Int(r, "venta_id"),
                    EmprendimientoId = Int(r, "emprendimiento_id"),
                    Fecha = Fec(r, "fecha"),
                    Observacion = Txt(r, "observacion"),
                    Productos = Int(r, "productos"),
                    Total = Dec(r, "total")
                },
                c => Entero(c, "emp", emprendimientoId));
        }

        public Venta? ObtenerConDetalles(int id)
        {
            Venta? venta = Uno("SELECT venta_id, emprendimiento_id, fecha, observacion, productos, total FROM vw_sg_ventas_resumen " +
                               "WHERE venta_id = :p_id",
                r => new Venta
                {
                    Id = Int(r, "venta_id"),
                    EmprendimientoId = Int(r, "emprendimiento_id"),
                    Fecha = Fec(r, "fecha"),
                    Observacion = Txt(r, "observacion"),
                    Productos = Int(r, "productos"),
                    Total = Dec(r, "total")
                },
                c => Entero(c, "id", id));
            if (venta == null) return null;

            venta.Detalles = Lista("SELECT producto_id, producto_nombre, cantidad, precio_unitario FROM sg_detalle_venta " +
                                   "WHERE venta_id = :p_id ORDER BY id",
                r => new DetalleVenta
                {
                    ProductoId = Int(r, "producto_id"),
                    ProductoNombre = Txt(r, "producto_nombre"),
                    Cantidad = Int(r, "cantidad"),
                    PrecioUnitario = Dec(r, "precio_unitario")
                },
                c => Entero(c, "id", id));
            return venta;
        }

        public int Registrar(int emprendimientoId, string observacion, List<DetalleVenta> carrito)
        {
            // El carrito viaja como JSON y Oracle lo lee con JSON_TABLE dentro de sp_sg_registrar_venta.
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < carrito.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append("{\"producto_id\":").Append(carrito[i].ProductoId.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"cantidad\":").Append(carrito[i].Cantidad.ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            json.Append(']');

            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_registrar_venta(:p_emp, :p_obs, :p_detalles, :p_venta); END;"))
                {
                    Entero(cmd, "emp", emprendimientoId);
                    Texto(cmd, "obs", observacion);
                    Param(cmd, "detalles", OracleDbType.Clob, json.ToString());
                    OracleParameter venta = Param(cmd, "venta", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(venta);
                }
            });
        }
    }

    public class MovimientoDaoOracle : IMovimientoDao
    {
        private const string Sql =
            "SELECT id, emprendimiento_id, fecha, tipo, categoria_id, categoria, descripcion, monto, referencia FROM vw_sg_movimientos";

        private static Movimiento Mapear(OracleDataReader r)
        {
            return new Movimiento
            {
                Id = Int(r, "id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                Fecha = Fec(r, "fecha"),
                Tipo = (TipoMovimiento)Enum.Parse(typeof(TipoMovimiento), Txt(r, "tipo")),
                CategoriaId = Int(r, "categoria_id"),
                Categoria = Txt(r, "categoria"),
                Descripcion = Txt(r, "descripcion"),
                Monto = Dec(r, "monto"),
                Referencia = Txt(r, "referencia")
            };
        }

        public List<Movimiento> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp ORDER BY fecha DESC, id DESC", Mapear, c => Entero(c, "emp", emprendimientoId));
        }

        public Movimiento? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Movimiento Insertar(Movimiento m)
        {
            // trg_sg_movimiento_valida comprueba que la categoria corresponda al tipo y que la fecha no sea futura.
            m.Id = InsertarConId(
                "INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto, referencia) " +
                "VALUES (:p_emp, :p_fecha, :p_tipo, :p_categoria, :p_descripcion, :p_monto, :p_referencia)",
                c =>
                {
                    Entero(c, "emp", m.EmprendimientoId);
                    Fecha(c, "fecha", m.Fecha);
                    Texto(c, "tipo", m.Tipo.ToString());
                    Entero(c, "categoria", m.CategoriaId);
                    Texto(c, "descripcion", m.Descripcion);
                    Numero(c, "monto", m.Monto);
                    Texto(c, "referencia", m.Referencia);
                });
            return m;
        }

        public bool Eliminar(int id)
        {
            // Solo movimientos manuales: los de ventas e inventario tienen referencia y no se borran.
            return EjecutarSql("DELETE FROM sg_movimiento WHERE id = :p_id AND referencia IS NULL", c => Entero(c, "id", id)) > 0;
        }
    }

    public class ObligacionSocioDaoOracle : IObligacionSocioDao
    {
        private const string Sql =
            "SELECT id, emprendimiento_id, movimiento_id, fecha_aporte, categoria, beneficiario, monto_aporte, porcentaje, " +
            "monto, estado, fecha_pago FROM vw_sg_obligaciones_socio";

        private static ObligacionSocio Mapear(OracleDataReader r)
        {
            return new ObligacionSocio
            {
                Id = Int(r, "id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                MovimientoId = Int(r, "movimiento_id"),
                FechaAporte = Fec(r, "fecha_aporte"),
                Categoria = Txt(r, "categoria"),
                Beneficiario = Txt(r, "beneficiario"),
                MontoAporte = Dec(r, "monto_aporte"),
                Porcentaje = Dec(r, "porcentaje"),
                Monto = Dec(r, "monto"),
                Estado = Txt(r, "estado"),
                FechaPago = FecNula(r, "fecha_pago")
            };
        }

        public List<ObligacionSocio> ListarPorEmprendimiento(int emprendimientoId)
        {
            // Primero las pendientes; dentro de cada grupo, las mas recientes
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp " +
                         "ORDER BY CASE estado WHEN 'pendiente' THEN 0 ELSE 1 END, fecha_aporte DESC, id DESC",
                         Mapear, c => Entero(c, "emp", emprendimientoId));
        }

        public ObligacionSocio? PorMovimiento(int movimientoId)
        {
            return Uno(Sql + " WHERE movimiento_id = :p_mov", Mapear, c => Entero(c, "mov", movimientoId));
        }

        public int RegistrarAporte(Movimiento ingreso, string beneficiario, decimal porcentaje, bool gastoInmediato)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con,
                    "BEGIN sp_sg_registrar_aporte(:p_emp, :p_categoria, :p_descripcion, :p_monto, :p_fecha, " +
                    ":p_beneficiario, :p_porcentaje, :p_inmediato, :p_movimiento, :p_obligacion); END;"))
                {
                    Entero(cmd, "emp", ingreso.EmprendimientoId);
                    Entero(cmd, "categoria", ingreso.CategoriaId);
                    Texto(cmd, "descripcion", ingreso.Descripcion);
                    Numero(cmd, "monto", ingreso.Monto);
                    Fecha(cmd, "fecha", ingreso.Fecha);
                    Texto(cmd, "beneficiario", beneficiario);
                    Numero(cmd, "porcentaje", porcentaje);
                    Texto(cmd, "inmediato", gastoInmediato ? "S" : "N");
                    OracleParameter movimiento = Param(cmd, "movimiento", OracleDbType.Int32, null, ParameterDirection.Output);
                    Param(cmd, "obligacion", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(movimiento);
                }
            });
        }

        public void Pagar(int emprendimientoId, int obligacionId, DateTime fecha)
        {
            Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_pagar_obligacion(:p_emp, :p_obligacion, :p_fecha, :p_movimiento); END;"))
                {
                    Entero(cmd, "emp", emprendimientoId);
                    Entero(cmd, "obligacion", obligacionId);
                    Fecha(cmd, "fecha", fecha);
                    Param(cmd, "movimiento", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                }
            });
        }
    }

    public class PresupuestoDaoOracle : IPresupuestoDao
    {
        private static Presupuesto Mapear(OracleDataReader r, int emprendimientoId)
        {
            return new Presupuesto
            {
                Id = Int(r, "presupuesto_id"),
                EmprendimientoId = emprendimientoId,
                CategoriaId = Int(r, "categoria_id"),
                Categoria = Txt(r, "categoria"),
                Mes = Txt(r, "mes"),
                MontoLimite = Dec(r, "monto_limite"),
                Gastado = Dec(r, "gastado"),
                Disponible = Dec(r, "disponible"),
                PorcentajeUso = Dec(r, "porcentaje_uso"),
                Estado = Txt(r, "estado")
            };
        }

        public List<Presupuesto> EstadoDelMes(int emprendimientoId, string mes)
        {
            return ListaCursor("BEGIN sp_sg_presupuestos_estado(:p_emp, :p_mes, :p_resultado); END;",
                r => Mapear(r, emprendimientoId),
                c => { Entero(c, "emp", emprendimientoId); Texto(c, "mes", mes); });
        }

        public Presupuesto? ObtenerPorId(int id)
        {
            return Uno("SELECT presupuesto_id, emprendimiento_id, categoria_id, categoria, mes, monto_limite, gastado, disponible, " +
                       "porcentaje_uso, estado FROM vw_sg_uso_presupuesto WHERE presupuesto_id = :p_id",
                r => Mapear(r, Int(r, "emprendimiento_id")), c => Entero(c, "id", id));
        }

        public Presupuesto Insertar(Presupuesto p)
        {
            // trg_sg_presupuesto_valida: mes yyyy-MM y solo categorias de gasto. uq_sg_pres: uno por categoria y mes.
            p.Id = InsertarConId(
                "INSERT INTO sg_presupuesto (emprendimiento_id, categoria_id, monto_limite, mes) VALUES (:p_emp, :p_cat, :p_monto, :p_mes)",
                c =>
                {
                    Entero(c, "emp", p.EmprendimientoId);
                    Entero(c, "cat", p.CategoriaId);
                    Numero(c, "monto", p.MontoLimite);
                    Texto(c, "mes", p.Mes);
                });
            return p;
        }

        public void ActualizarLimite(int id, decimal montoLimite)
        {
            EjecutarSql("UPDATE sg_presupuesto SET monto_limite = :p_monto WHERE id = :p_id",
                c => { Numero(c, "monto", montoLimite); Entero(c, "id", id); });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_presupuesto WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }
    }
}
