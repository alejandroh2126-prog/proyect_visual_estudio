using System;
using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    public class MovimientoDaoOracle : DaoOracle<Movimiento>, IMovimientoDao
    {
        protected override string Tabla { get { return "sg_movimiento"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "emprendimiento_id", "fecha", "tipo", "categoria", "descripcion", "monto", "referencia" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, emprendimiento_id, fecha, tipo, categoria, descripcion, monto, referencia FROM sg_movimiento"; }
        }

        protected override Movimiento Mapear(DbDataReader r)
        {
            return new Movimiento
            {
                Id = ConexionOracle.Entero(r, "id"),
                EmprendimientoId = ConexionOracle.Entero(r, "emprendimiento_id"),
                Fecha = ConexionOracle.Fecha(r, "fecha"),
                Tipo = (TipoMovimiento)Enum.Parse(typeof(TipoMovimiento), ConexionOracle.Texto(r, "tipo")),
                Categoria = ConexionOracle.Texto(r, "categoria"),
                Descripcion = ConexionOracle.Texto(r, "descripcion"),
                Monto = ConexionOracle.Decimal(r, "monto"),
                Referencia = ConexionOracle.Texto(r, "referencia")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Movimiento m)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, m.EmprendimientoId);
            ConexionOracle.Param(cmd, "fecha", DbType.DateTime, m.Fecha);
            ConexionOracle.Param(cmd, "tipo", DbType.String, m.Tipo.ToString());
            ConexionOracle.Param(cmd, "categoria", DbType.String, m.Categoria);
            ConexionOracle.Param(cmd, "descripcion", DbType.String, m.Descripcion);
            ConexionOracle.Param(cmd, "monto", DbType.Decimal, m.Monto);
            ConexionOracle.Param(cmd, "referencia", DbType.String, m.Referencia);
        }

        protected override int ObtenerId(Movimiento m) { return m.Id; }
        protected override void AsignarId(Movimiento m, int id) { m.Id = id; }

        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }
    }
}
