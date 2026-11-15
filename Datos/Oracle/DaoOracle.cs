using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Logica.Utilidades;

namespace Datos.Oracle
{
    /// <summary>
    /// PATRON DAO (implementacion Oracle). La logica comun de guardar y leer vive aqui UNA vez.
    /// Cada DAO hijo solo dice: cual es su tabla, que columnas tiene, como se arma la entidad desde una fila
    /// y como se envian sus valores. Todo el SQL usa parametros (:p_nombre) para evitar inyeccion de SQL.
    /// </summary>
    public abstract class DaoOracle<T> : IDao<T> where T : class
    {
        /// <summary>Nombre de la tabla (ej: sg_producto).</summary>
        protected abstract string Tabla { get; }

        /// <summary>Columnas que se escriben en INSERT y UPDATE (sin el id, que genera Oracle).</summary>
        protected abstract string[] ColumnasEscritura { get; }

        /// <summary>SELECT completo (sin WHERE ni ORDER BY).</summary>
        protected abstract string SqlSeleccion { get; }

        /// <summary>Nombre de la columna id tal como aparece en SqlSeleccion (puede llevar alias de tabla).</summary>
        protected virtual string ColumnaId { get { return "id"; } }

        protected abstract T Mapear(DbDataReader lector);
        protected abstract void AsignarParametros(DbCommand comando, T entidad);
        protected abstract int ObtenerId(T entidad);
        protected abstract void AsignarId(T entidad, int id);

        public virtual List<T> Listar()
        {
            return ConexionOracle.Ejecutar(con => Consultar(con, SqlSeleccion + " ORDER BY " + ColumnaId, null));
        }

        public virtual T? ObtenerPorId(int id)
        {
            List<T> lista = ConexionOracle.Ejecutar(con =>
                Consultar(con, SqlSeleccion + " WHERE " + ColumnaId + " = :p_id", cmd =>
                    ConexionOracle.Param(cmd, "id", DbType.Int32, id)));
            return lista.Count > 0 ? lista[0] : null;
        }

        public virtual T Insertar(T entidad)
        {
            ConexionOracle.Ejecutar(con => InsertarCon(con, entidad));
            return entidad;
        }

        public virtual void Actualizar(T entidad)
        {
            ConexionOracle.Ejecutar(con => ActualizarCon(con, entidad));
        }

        public virtual bool Eliminar(int id)
        {
            return ConexionOracle.Ejecutar(con =>
            {
                using (DbCommand cmd = ConexionOracle.Comando(con, "DELETE FROM " + Tabla + " WHERE id = :p_id"))
                {
                    ConexionOracle.Param(cmd, "id", DbType.Int32, id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            });
        }

        /// <summary>Elimina todas las filas de un emprendimiento (para las tablas que tienen emprendimiento_id).</summary>
        protected int EliminarPorEmprendimientoId(int emprendimientoId)
        {
            return ConexionOracle.Ejecutar(con =>
            {
                using (DbCommand cmd = ConexionOracle.Comando(con,
                    "DELETE FROM " + Tabla + " WHERE emprendimiento_id = :p_emprendimiento"))
                {
                    ConexionOracle.Param(cmd, "emprendimiento", DbType.Int32, emprendimientoId);
                    return cmd.ExecuteNonQuery();
                }
            });
        }

        // ---------- Piezas reutilizables (tambien las usan los DAOs que necesitan transaccion) ----------

        protected List<T> Consultar(DbConnection con, string sql, System.Action<DbCommand>? parametros)
        {
            List<T> lista = new List<T>();
            using (DbCommand cmd = ConexionOracle.Comando(con, sql))
            {
                if (parametros != null) parametros(cmd);
                using (DbDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read()) lista.Add(Mapear(lector));
                }
            }
            return lista;
        }

        protected void InsertarCon(DbConnection con, T entidad, DbTransaction? transaccion = null)
        {
            string columnas = string.Join(", ", ColumnasEscritura);
            string valores = ":p_" + string.Join(", :p_", ColumnasEscritura);
            string sql = "INSERT INTO " + Tabla + " (" + columnas + ") VALUES (" + valores + ") RETURNING id INTO :p_id";

            using (DbCommand cmd = ConexionOracle.Comando(con, sql))
            {
                cmd.Transaction = transaccion;
                AsignarParametros(cmd, entidad);
                DbParameter salida = ConexionOracle.Param(cmd, "id", DbType.Int32, null, ParameterDirection.Output);
                cmd.ExecuteNonQuery();
                AsignarId(entidad, int.Parse(salida.Value!.ToString()!));
            }
        }

        protected void ActualizarCon(DbConnection con, T entidad, DbTransaction? transaccion = null)
        {
            List<string> asignaciones = new List<string>();
            foreach (string columna in ColumnasEscritura) asignaciones.Add(columna + " = :p_" + columna);
            string sql = "UPDATE " + Tabla + " SET " + string.Join(", ", asignaciones) + " WHERE id = :p_id";

            using (DbCommand cmd = ConexionOracle.Comando(con, sql))
            {
                cmd.Transaction = transaccion;
                AsignarParametros(cmd, entidad);
                ConexionOracle.Param(cmd, "id", DbType.Int32, ObtenerId(entidad));
                if (cmd.ExecuteNonQuery() == 0)
                    throw new ReglaDeNegocioException("No se encontró el registro que se quiere actualizar.");
            }
        }
    }
}
