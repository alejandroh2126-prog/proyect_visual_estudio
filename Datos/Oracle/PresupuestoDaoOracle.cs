using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    public class PresupuestoDaoOracle : DaoOracle<Presupuesto>, IPresupuestoDao
    {
        protected override string Tabla { get { return "sg_presupuesto"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "emprendimiento_id", "categoria", "monto_limite", "mes" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, emprendimiento_id, categoria, monto_limite, mes FROM sg_presupuesto"; }
        }

        protected override Presupuesto Mapear(DbDataReader r)
        {
            return new Presupuesto
            {
                Id = ConexionOracle.Entero(r, "id"),
                EmprendimientoId = ConexionOracle.Entero(r, "emprendimiento_id"),
                Categoria = ConexionOracle.Texto(r, "categoria"),
                MontoLimite = ConexionOracle.Decimal(r, "monto_limite"),
                Mes = ConexionOracle.Texto(r, "mes")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Presupuesto p)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, p.EmprendimientoId);
            ConexionOracle.Param(cmd, "categoria", DbType.String, p.Categoria);
            ConexionOracle.Param(cmd, "monto_limite", DbType.Decimal, p.MontoLimite);
            ConexionOracle.Param(cmd, "mes", DbType.String, p.Mes);
        }

        protected override int ObtenerId(Presupuesto p) { return p.Id; }
        protected override void AsignarId(Presupuesto p, int id) { p.Id = id; }

        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }
    }
}
