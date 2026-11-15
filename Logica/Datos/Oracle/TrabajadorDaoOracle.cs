using System.Data;
using System.Data.Common;
using Entidades;

namespace Logica.Datos.Oracle
{
    /// <summary>
    /// El trabajador trae su cargo en la misma consulta (JOIN), igual que el DAO CSV resuelve el cargo
    /// al leer: un trabajador cuyo cargo ya no existe no aparece.
    /// </summary>
    public class TrabajadorDaoOracle : DaoOracle<Trabajador>, ITrabajadorDao
    {
        protected override string Tabla { get { return "sg_trabajador"; } }
        protected override string ColumnaId { get { return "t.id"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "emprendimiento_id", "cargo_id", "nombre", "apellido", "cedula", "fecha_ingreso", "estado" }; }
        }
        protected override string SqlSeleccion
        {
            get
            {
                return "SELECT t.id, t.emprendimiento_id, t.nombre, t.apellido, t.cedula, t.fecha_ingreso, t.estado, " +
                       "c.id AS cargo_id, c.emprendimiento_id AS cargo_emprendimiento_id, c.nombre AS cargo_nombre, " +
                       "c.salario_base AS cargo_salario_base, c.descripcion AS cargo_descripcion " +
                       "FROM sg_trabajador t JOIN sg_cargo c ON c.id = t.cargo_id";
            }
        }

        protected override Trabajador Mapear(DbDataReader r)
        {
            Cargo cargo = new Cargo
            {
                Id = ConexionOracle.Entero(r, "cargo_id"),
                EmprendimientoId = ConexionOracle.Entero(r, "cargo_emprendimiento_id"),
                Nombre = ConexionOracle.Texto(r, "cargo_nombre"),
                SalarioBase = ConexionOracle.Decimal(r, "cargo_salario_base"),
                Descripcion = ConexionOracle.Texto(r, "cargo_descripcion")
            };

            Trabajador t = new Trabajador(ConexionOracle.Entero(r, "id"), ConexionOracle.Texto(r, "nombre"),
                ConexionOracle.Texto(r, "apellido"), ConexionOracle.Texto(r, "cedula"), cargo,
                ConexionOracle.Fecha(r, "fecha_ingreso"), ConexionOracle.Entero(r, "emprendimiento_id"));
            t.Estado = ConexionOracle.Texto(r, "estado");
            return t;
        }

        protected override void AsignarParametros(DbCommand cmd, Trabajador t)
        {
            ConexionOracle.Param(cmd, "emprendimiento_id", DbType.Int32, t.EmprendimientoId);
            ConexionOracle.Param(cmd, "cargo_id", DbType.Int32, t.Cargo.Id);
            ConexionOracle.Param(cmd, "nombre", DbType.String, t.Nombre);
            ConexionOracle.Param(cmd, "apellido", DbType.String, t.Apellido);
            ConexionOracle.Param(cmd, "cedula", DbType.String, t.Cedula);
            ConexionOracle.Param(cmd, "fecha_ingreso", DbType.DateTime, t.FechaIngreso);
            ConexionOracle.Param(cmd, "estado", DbType.String, t.Estado);
        }

        protected override int ObtenerId(Trabajador t) { return t.Id; }
        protected override void AsignarId(Trabajador t, int id) { t.Id = id; }

        public int EliminarPorEmprendimiento(int emprendimientoId) { return EliminarPorEmprendimientoId(emprendimientoId); }
    }
}
