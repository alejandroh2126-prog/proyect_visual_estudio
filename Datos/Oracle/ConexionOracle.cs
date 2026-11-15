using System;
using System.Data;
using System.Data.Common;
using Logica.Utilidades;
using Oracle.ManagedDataAccess.Client;

namespace Datos.Oracle
{
    /// <summary>
    /// UNA SOLA MISION: abrir conexiones a Oracle y traducir sus errores a mensajes que el usuario entienda.
    /// Todos los DAOs de Oracle piden la conexion aqui; ninguno conoce la cadena de conexion.
    /// Funciona con Oracle local (XE/Free) y con Autonomous Database (wallet).
    /// </summary>
    public static class ConexionOracle
    {
        private static string _cadena = string.Empty;

        /// <summary>Guarda la configuracion. Se llama una vez, al armar la fabrica de DAOs.</summary>
        public static void Configurar(ConfiguracionBD config)
        {
            if (string.IsNullOrWhiteSpace(config.Usuario) || string.IsNullOrWhiteSpace(config.DataSource))
                throw new ReglaDeNegocioException("Falta el usuario o el dataSource en conexion.json.");

            if (!string.IsNullOrWhiteSpace(config.CarpetaWallet))
            {
                // Autonomous Database: la carpeta del wallet trae tnsnames.ora y cwallet.sso.
                OracleConfiguration.TnsAdmin = config.CarpetaWallet;
                OracleConfiguration.WalletLocation = config.CarpetaWallet;
            }

            OracleConnectionStringBuilder cadena = new OracleConnectionStringBuilder();
            cadena.UserID = config.Usuario;
            cadena.Password = config.Clave;
            cadena.DataSource = config.DataSource;
            cadena.ConnectionTimeout = 20;
            _cadena = cadena.ConnectionString;
        }

        /// <summary>Abre una conexion nueva (el "pool" de Oracle la reutiliza, es barata).</summary>
        public static DbConnection Abrir()
        {
            OracleConnection conexion = new OracleConnection(_cadena);
            conexion.Open();
            return conexion;
        }

        /// <summary>Ejecuta una accion con una conexion abierta y la cierra al terminar.</summary>
        public static T Ejecutar<T>(Func<DbConnection, T> accion)
        {
            try
            {
                using (DbConnection conexion = Abrir())
                {
                    return accion(conexion);
                }
            }
            catch (OracleException ex)
            {
                throw Traducir(ex);
            }
        }

        public static void Ejecutar(Action<DbConnection> accion)
        {
            Ejecutar<bool>(con => { accion(con); return true; });
        }

        /// <summary>Crea un comando que enlaza los parametros POR NOMBRE (no por posicion).</summary>
        public static DbCommand Comando(DbConnection conexion, string sql)
        {
            OracleCommand comando = ((OracleConnection)conexion).CreateCommand();
            comando.BindByName = true;
            comando.CommandText = sql;
            return comando;
        }

        /// <summary>
        /// Agrega un parametro. El nombre se escribe sin prefijo: Param(cmd, "nombre", ...) se usa en el SQL como :p_nombre.
        /// Oracle trata el texto vacio como NULL, por eso aqui se envia NULL explicitamente.
        /// </summary>
        public static DbParameter Param(DbCommand comando, string nombre, DbType tipo, object? valor,
                                        ParameterDirection direccion = ParameterDirection.Input)
        {
            DbParameter p = comando.CreateParameter();
            p.ParameterName = "p_" + nombre;
            p.DbType = tipo;
            p.Direction = direccion;
            if (valor == null || (valor is string texto && texto.Length == 0)) p.Value = DBNull.Value;
            else p.Value = valor;
            comando.Parameters.Add(p);
            return p;
        }

        // ---------- Lectura de columnas (por nombre) ----------

        public static string Texto(DbDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? string.Empty : lector.GetString(i);
        }

        public static int Entero(DbDataReader lector, string columna)
        {
            return Convert.ToInt32(lector.GetValue(lector.GetOrdinal(columna)));
        }

        public static decimal Decimal(DbDataReader lector, string columna)
        {
            return Convert.ToDecimal(lector.GetValue(lector.GetOrdinal(columna)));
        }

        public static DateTime Fecha(DbDataReader lector, string columna)
        {
            return lector.GetDateTime(lector.GetOrdinal(columna));
        }

        // ---------- Verificacion y errores ----------

        /// <summary>Comprueba que se puede conectar y que las tablas de SGAPE existen.</summary>
        public static void Probar()
        {
            try
            {
                using (DbConnection conexion = Abrir())
                using (DbCommand cmd = Comando(conexion, "SELECT COUNT(*) FROM sg_usuario"))
                {
                    cmd.ExecuteScalar();
                }
            }
            catch (OracleException ex)
            {
                if (ex.Number == 942)
                    throw new ReglaDeNegocioException(
                        "Se conectó a Oracle pero no existen las tablas de SGAPE. Ejecuta el script sql/sgape_oracle.sql.");
                throw new ReglaDeNegocioException("No se pudo conectar a Oracle: " + ex.Message);
            }
            catch (Exception ex) when (!(ex is ReglaDeNegocioException))
            {
                // Por ejemplo, una carpeta de wallet que no existe.
                throw new ReglaDeNegocioException("No se pudo conectar a Oracle: " + ex.Message);
            }
        }

        private static ReglaDeNegocioException Traducir(OracleException ex)
        {
            if (ex.Number == 1)
                return new ReglaDeNegocioException("Ya existe un registro con esos datos (valor repetido).");
            if (ex.Number == 2292)
                return new ReglaDeNegocioException("No se puede eliminar: otros registros dependen de este.");
            return new ReglaDeNegocioException("Error en la base de datos Oracle: " + ex.Message);
        }
    }
}
