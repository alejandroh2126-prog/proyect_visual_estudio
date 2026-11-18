using System;
using System.Collections.Generic;
using System.Data;
using Entidades;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Datos.Oracle
{
    /// <summary>
    /// UNA SOLA MISION: hablar con Oracle. Abre conexiones, arma comandos con parametros
    /// (nunca concatena valores: evita la inyeccion de SQL), lee cursores (SYS_REFCURSOR)
    /// y traduce los errores de Oracle a mensajes que el usuario entienda.
    /// Ningun otro DAO conoce la cadena de conexion.
    /// </summary>
    public static class ConexionOracle
    {
        private static string _cadena = string.Empty;

        public static void Configurar(ConfiguracionBD config)
        {
            if (string.IsNullOrWhiteSpace(config.Usuario) || string.IsNullOrWhiteSpace(config.DataSource))
                throw new ReglaDeNegocioException("Falta el usuario o el dataSource de la conexión a Oracle.");

            if (!string.IsNullOrWhiteSpace(config.CarpetaWallet))
            {
                // Autonomous Database: la carpeta del wallet trae tnsnames.ora y cwallet.sso
                OracleConfiguration.TnsAdmin = config.CarpetaWallet;
                OracleConfiguration.WalletLocation = config.CarpetaWallet;
            }

            OracleConnectionStringBuilder cadena = new OracleConnectionStringBuilder
            {
                UserID = config.Usuario,
                Password = config.Clave,
                DataSource = config.DataSource,
                ConnectionTimeout = 20
            };
            _cadena = cadena.ConnectionString;
        }

        public static OracleConnection Abrir()
        {
            OracleConnection conexion = new OracleConnection(_cadena);
            conexion.Open();
            return conexion;
        }

        /// <summary>Ejecuta una accion con una conexion abierta, la cierra al final y traduce los errores.</summary>
        public static T Ejecutar<T>(Func<OracleConnection, T> accion)
        {
            try
            {
                using (OracleConnection conexion = Abrir())
                {
                    return accion(conexion);
                }
            }
            catch (OracleException ex)
            {
                throw Traducir(ex);
            }
        }

        public static void Ejecutar(Action<OracleConnection> accion)
        {
            Ejecutar<bool>(con => { accion(con); return true; });
        }

        /// <summary>Comando que enlaza parametros POR NOMBRE. En el SQL se escriben como :p_nombre.</summary>
        public static OracleCommand Comando(OracleConnection conexion, string sql)
        {
            OracleCommand comando = conexion.CreateCommand();
            comando.BindByName = true;
            comando.CommandText = sql;
            return comando;
        }

        /// <summary>Agrega el parametro :p_nombre. Oracle trata el texto vacio como NULL, por eso se envia NULL.</summary>
        public static OracleParameter Param(OracleCommand comando, string nombre, OracleDbType tipo, object? valor,
                                            ParameterDirection direccion = ParameterDirection.Input)
        {
            OracleParameter p = new OracleParameter("p_" + nombre, tipo)
            {
                Direction = direccion,
                Value = (valor == null || (valor is string texto && texto.Length == 0)) ? DBNull.Value : valor
            };
            if (tipo == OracleDbType.Varchar2 && direccion != ParameterDirection.Input) p.Size = 4000;
            comando.Parameters.Add(p);
            return p;
        }

        public static OracleParameter Texto(OracleCommand c, string nombre, string? valor) { return Param(c, nombre, OracleDbType.Varchar2, valor); }
        public static OracleParameter Entero(OracleCommand c, string nombre, int? valor) { return Param(c, nombre, OracleDbType.Int32, valor); }
        public static OracleParameter Numero(OracleCommand c, string nombre, decimal valor) { return Param(c, nombre, OracleDbType.Decimal, valor); }
        public static OracleParameter Largo(OracleCommand c, string nombre, long? valor) { return Param(c, nombre, OracleDbType.Int64, valor); }
        public static OracleParameter Fecha(OracleCommand c, string nombre, DateTime? valor) { return Param(c, nombre, OracleDbType.Date, valor); }

        // ---------- Consultas listas para usar ----------

        public static List<T> Lista<T>(string sql, Func<OracleDataReader, T> mapear, Action<OracleCommand>? parametros = null)
        {
            return Ejecutar(con =>
            {
                List<T> lista = new List<T>();
                using (OracleCommand cmd = Comando(con, sql))
                {
                    parametros?.Invoke(cmd);
                    using (OracleDataReader lector = cmd.ExecuteReader())
                    {
                        while (lector.Read()) lista.Add(mapear(lector));
                    }
                }
                return lista;
            });
        }

        public static T? Uno<T>(string sql, Func<OracleDataReader, T> mapear, Action<OracleCommand>? parametros = null) where T : class
        {
            List<T> lista = Lista(sql, mapear, parametros);
            return lista.Count > 0 ? lista[0] : null;
        }

        /// <summary>
        /// Ejecuta un bloque PL/SQL que devuelve un SYS_REFCURSOR en el parametro :p_resultado
        /// (por ejemplo "BEGIN sp_sg_productos_stock_bajo(:p_emp, :p_resultado); END;") y lee sus filas.
        /// </summary>
        public static List<T> ListaCursor<T>(string bloque, Func<OracleDataReader, T> mapear, Action<OracleCommand>? parametros = null)
        {
            return Ejecutar(con =>
            {
                List<T> lista = new List<T>();
                using (OracleCommand cmd = Comando(con, bloque))
                {
                    parametros?.Invoke(cmd);
                    OracleParameter cursor = Param(cmd, "resultado", OracleDbType.RefCursor, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();

                    if (cursor.Value is OracleRefCursor refCursor && !refCursor.IsNull)
                    {
                        using (OracleDataReader lector = refCursor.GetDataReader())
                        {
                            while (lector.Read()) lista.Add(mapear(lector));
                        }
                    }
                }
                return lista;
            });
        }

        /// <summary>Valor de una funcion: "SELECT pkg_sg_nomina.nomina_neta(:p_emp) FROM dual".</summary>
        public static decimal Valor(string sql, Action<OracleCommand>? parametros = null)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, sql))
                {
                    parametros?.Invoke(cmd);
                    object? valor = cmd.ExecuteScalar();
                    if (valor == null || valor == DBNull.Value) return 0m;
                    if (valor is OracleDecimal od) return OracleDecimal.SetPrecision(od, 28).Value;
                    return Convert.ToDecimal(valor);
                }
            });
        }

        /// <summary>INSERT/UPDATE/DELETE o bloque PL/SQL. Devuelve las filas afectadas.</summary>
        public static int EjecutarSql(string sql, Action<OracleCommand>? parametros = null)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, sql))
                {
                    parametros?.Invoke(cmd);
                    return cmd.ExecuteNonQuery();
                }
            });
        }

        /// <summary>INSERT ... RETURNING id INTO :p_id. Devuelve el id que genero Oracle (IDENTITY).</summary>
        public static int InsertarConId(string sqlInsert, Action<OracleCommand> parametros)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, sqlInsert + " RETURNING id INTO :p_id"))
                {
                    parametros(cmd);
                    OracleParameter id = Param(cmd, "id", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(id);
                }
            });
        }

        /// <summary>Lee un parametro OUT numerico despues de ejecutar.</summary>
        public static int SalidaEntero(OracleParameter p)
        {
            if (p.Value == null || p.Value == DBNull.Value) return 0;
            if (p.Value is OracleDecimal od) return od.IsNull ? 0 : od.ToInt32();
            return Convert.ToInt32(p.Value.ToString());
        }

        public static string SalidaTexto(OracleParameter p)
        {
            if (p.Value is OracleString os) return os.IsNull ? string.Empty : os.Value;
            return p.Value?.ToString() ?? string.Empty;
        }

        // ---------- Lectura de columnas (por nombre) ----------

        public static string Txt(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? string.Empty : r.GetString(i);
        }

        public static int Int(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? 0 : r.GetOracleDecimal(i).ToInt32();
        }

        public static int? IntNulo(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? (int?)null : r.GetOracleDecimal(i).ToInt32();
        }

        public static long? LongNulo(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? (long?)null : r.GetOracleDecimal(i).ToInt64();
        }

        /// <summary>Lee un NUMBER sin desbordar el decimal de .NET (Oracle tiene 38 digitos, .NET 28).</summary>
        public static decimal Dec(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            if (r.IsDBNull(i)) return 0m;
            return OracleDecimal.SetPrecision(r.GetOracleDecimal(i), 28).Value;
        }

        public static DateTime Fec(OracleDataReader r, string columna)
        {
            return r.GetDateTime(r.GetOrdinal(columna));
        }

        public static DateTime? FecNula(OracleDataReader r, string columna)
        {
            int i = r.GetOrdinal(columna);
            return r.IsDBNull(i) ? (DateTime?)null : r.GetDateTime(i);
        }

        public static bool SiNo(OracleDataReader r, string columna)
        {
            return Txt(r, columna) == "S";
        }

        // ---------- Verificacion ----------

        /// <summary>Comprueba la conexion y que los objetos de SGAPE v2 existan y esten VALID.</summary>
        public static void Probar()
        {
            try
            {
                using (OracleConnection con = Abrir())
                using (OracleCommand cmd = Comando(con,
                    // all_objects: funciona con el dueño (ADMIN) y con un usuario con permisos (SGAPE_BOT)
                    "SELECT COUNT(DISTINCT object_name) FROM all_objects " +
                    "WHERE object_name IN ('PKG_SG_NOMINA', 'PKG_SG_REPORTES', 'PKG_SG_TELEGRAM', 'SP_SG_PRESUPUESTOS_ESTADO', " +
                    "'VW_SG_USO_PRESUPUESTO', 'VW_SG_CIUDADES') " +
                    "AND object_type IN ('PACKAGE', 'PROCEDURE', 'VIEW') AND status = 'VALID'"))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) < 6)
                        throw new ReglaDeNegocioException(
                            "La base de datos no tiene los objetos de SGAPE v2 o alguno está inválido. " +
                            "Ejecuta sql/sgape_v2_completo.sql en SQL Developer.");
                }
            }
            catch (OracleException ex)
            {
                throw new ReglaDeNegocioException("No se pudo conectar a Oracle: " + PrimeraLinea(ex.Message));
            }
            catch (Exception ex) when (!(ex is ReglaDeNegocioException))
            {
                throw new ReglaDeNegocioException("No se pudo conectar a Oracle: " + ex.Message);
            }
        }

        // ---------- Errores ----------

        /// <summary>Mensajes para las restricciones UNIQUE y CHECK (el nombre de la restriccion dice cual fallo).</summary>
        private static readonly Dictionary<string, string> Restricciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "UQ_SG_USUARIO_EMAIL", "Este correo ya está registrado." },
            { "CK_SG_USUARIO_EMAIL", "El correo no tiene un formato válido." },
            { "UQ_SG_EMP_NOMBRE", "Ya tienes un emprendimiento con ese nombre." },
            { "UQ_SG_CARGO_NOMBRE", "Ya existe un cargo con ese nombre en este emprendimiento." },
            { "UQ_SG_TRAB_DOC", "Ya existe un trabajador con ese documento en este emprendimiento." },
            { "UQ_SG_PROD_NOMBRE", "Ya existe un producto con ese nombre." },
            { "UQ_SG_PRES", "Ya existe un presupuesto para esa categoría en ese mes." },
            { "UQ_SG_POST_DOC", "Ya te postulaste a esta vacante con ese documento." },
            { "CK_SG_TRAB_TEL", "El teléfono debe tener entre 7 y 10 números." },
            { "CK_SG_POST_TEL", "El teléfono debe tener entre 7 y 10 números." },
            { "CK_SG_PROD_PRECIOS", "Los precios no son válidos (el de venta debe ser mayor que cero)." },
            { "CK_SG_PROD_STOCK", "El stock no puede ser negativo." },
            { "CK_SG_MOV_MONTO", "El monto debe ser mayor que cero." },
            { "CK_SG_PRES_MONTO", "El límite debe ser mayor que cero." },
            { "CK_SG_CARGO_SALARIO", "El salario debe ser mayor que cero." },
            { "CK_SG_VAC_FECHAS", "La fecha de cierre no puede ser anterior a la publicación." },
            { "CK_SG_POST_TIPO_CV", "La hoja de vida debe ser PDF o Word (.docx)." }
        };

        public static ReglaDeNegocioException Traducir(OracleException ex)
        {
            // ORA-20000 a ORA-20999: errores de negocio lanzados por los procedimientos y disparadores
            if (ex.Number >= 20000 && ex.Number <= 20999)
                return new ReglaDeNegocioException(MensajeDeAplicacion(ex.Message));

            if (ex.Number == 1 || ex.Number == 2290)
            {
                foreach (KeyValuePair<string, string> r in Restricciones)
                    if (ex.Message.IndexOf(r.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return new ReglaDeNegocioException(r.Value);
                return new ReglaDeNegocioException(ex.Number == 1
                    ? "Ya existe un registro con esos datos."
                    : "Un dato no cumple las reglas de la base de datos.");
            }
            if (ex.Number == 2292) return new ReglaDeNegocioException("No se puede eliminar: hay otros registros que dependen de este (por ejemplo, trabajadores en un cargo).");
            if (ex.Number == 2291) return new ReglaDeNegocioException("Uno de los valores elegidos ya no existe. Recarga la pantalla.");
            if (ex.Number == 12899) return new ReglaDeNegocioException("Un texto es demasiado largo para el campo.");
            if (ex.Number == 1400) return new ReglaDeNegocioException("Falta un dato obligatorio.");
            if (ex.Number == 12170 || ex.Number == 12541 || ex.Number == 12514 || ex.Number == 3113 || ex.Number == 3114)
                return new ReglaDeNegocioException("No hay conexión con la base de datos. Revisa tu internet e inténtalo de nuevo.");
            return new ReglaDeNegocioException("Error en la base de datos: " + PrimeraLinea(ex.Message));
        }

        /// <summary>"ORA-20204: Stock insuficiente...\nORA-06512: en linea 12" -> "Stock insuficiente..."</summary>
        private static string MensajeDeAplicacion(string mensaje)
        {
            string primera = PrimeraLinea(mensaje);
            int dosPuntos = primera.IndexOf(':');
            return dosPuntos >= 0 ? primera.Substring(dosPuntos + 1).Trim() : primera;
        }

        private static string PrimeraLinea(string texto)
        {
            int salto = texto.IndexOf('\n');
            return (salto >= 0 ? texto.Substring(0, salto) : texto).Trim();
        }
    }
}
