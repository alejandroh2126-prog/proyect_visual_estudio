using System.Data;
using System.Data.Common;
using Entidades;

namespace Datos.Oracle
{
    public class UsuarioDaoOracle : DaoOracle<Usuario>, IUsuarioDao
    {
        protected override string Tabla { get { return "sg_usuario"; } }
        protected override string[] ColumnasEscritura
        {
            get { return new[] { "nombre", "email", "clave_hash", "fecha_registro" }; }
        }
        protected override string SqlSeleccion
        {
            get { return "SELECT id, nombre, email, clave_hash, fecha_registro FROM sg_usuario"; }
        }

        protected override Usuario Mapear(DbDataReader r)
        {
            return new Usuario
            {
                Id = ConexionOracle.Entero(r, "id"),
                Nombre = ConexionOracle.Texto(r, "nombre"),
                Email = ConexionOracle.Texto(r, "email"),
                ClaveHash = ConexionOracle.Texto(r, "clave_hash"),
                FechaRegistro = ConexionOracle.Fecha(r, "fecha_registro")
            };
        }

        protected override void AsignarParametros(DbCommand cmd, Usuario u)
        {
            ConexionOracle.Param(cmd, "nombre", DbType.String, u.Nombre);
            ConexionOracle.Param(cmd, "email", DbType.String, u.Email);
            ConexionOracle.Param(cmd, "clave_hash", DbType.String, u.ClaveHash);
            ConexionOracle.Param(cmd, "fecha_registro", DbType.DateTime, u.FechaRegistro);
        }

        protected override int ObtenerId(Usuario u) { return u.Id; }
        protected override void AsignarId(Usuario u, int id) { u.Id = id; }
    }
}
