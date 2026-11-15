using System.Data;
using System.Data.Common;
using Entidades;

namespace Logica.Datos.Oracle
{
    public class EmprendimientoDaoOracle : DaoOracle<Emprendimiento>, IEmprendimientoDao
    {
        protected override string Tabla { get { return "sg_emprendimiento"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "usuario_id", "nombre", "descripcion", "sector", "fecha_inicio", "estado" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, usuario_id, nombre, descripcion, sector, fecha_inicio, estado FROM sg_emprendimiento"; }
        }

        protected override Emprendimiento Mapear(DbDataReader r)
        {
            return new Emprendimiento
            {
                Id = ConexionOracle.Entero(r, "id"),
                UsuarioId = ConexionOracle.Entero(r, "usuario_id"),
                Nombre = ConexionOracle.Texto(r, "nombre"),
                Descripcion = ConexionOracle.Texto(r, "descripcion"),
                Sector = ConexionOracle.Texto(r, "sector"),
                FechaInicio = ConexionOracle.Fecha(r, "fecha_inicio"),
                Estado = ConexionOracle.Texto(r, "estado")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Emprendimiento e)
        {
            ConexionOracle.Param(cmd, "usuario_id", DbType.Int32, e.UsuarioId);
            ConexionOracle.Param(cmd, "nombre", DbType.String, e.Nombre);
            ConexionOracle.Param(cmd, "descripcion", DbType.String, e.Descripcion);
            ConexionOracle.Param(cmd, "sector", DbType.String, e.Sector);
            ConexionOracle.Param(cmd, "fecha_inicio", DbType.DateTime, e.FechaInicio);
            ConexionOracle.Param(cmd, "estado", DbType.String, e.Estado);
        }

        protected override int ObtenerId(Emprendimiento e) { return e.Id; }
        protected override void AsignarId(Emprendimiento e, int id) { e.Id = id; }
    }
}
