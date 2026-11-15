using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    public class CargoDaoOracle : DaoOracle<Cargo>, ICargoDao
    {
        protected override string Tabla { get { return "sg_cargo"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "emprendimiento_id", "nombre", "salario_base", "descripcion" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, emprendimiento_id, nombre, salario_base, descripcion FROM sg_cargo"; }
        }

        protected override Cargo Mapear(DbDataReader r)
        {
            return new Cargo
            {
                Id = ConexionOracle.Entero(r, "id"),
                EmprendimientoId = ConexionOracle.Entero(r, "emprendimiento_id"),
                Nombre = ConexionOracle.Texto(r, "nombre"),
                SalarioBase = ConexionOracle.Decimal(r, "salario_base"),
                Descripcion = ConexionOracle.Texto(r, "descripcion")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Cargo c)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, c.EmprendimientoId);
            ConexionOracle.Param(cmd, "nombre", DbType.String, c.Nombre);
            ConexionOracle.Param(cmd, "salario_base", DbType.Decimal, c.SalarioBase);
            ConexionOracle.Param(cmd, "descripcion", DbType.String, c.Descripcion);
        }

        protected override int ObtenerId(Cargo c) { return c.Id; }
        protected override void AsignarId(Cargo c, int id) { c.Id = id; }

        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }
    }
}
