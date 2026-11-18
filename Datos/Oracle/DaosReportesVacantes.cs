using System;
using System.Collections.Generic;
using System.Data;
using Entidades;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using static Datos.Oracle.ConexionOracle;

namespace Datos.Oracle
{
    public class VacanteDaoOracle : IVacanteDao
    {
        private const string Sql =
            "SELECT id, emprendimiento_id, emprendimiento, cargo_id, cargo, salario_base, titulo, descripcion, ciudad_codigo, " +
            "ciudad, fecha_publicacion, fecha_cierre, estado, postulaciones FROM vw_sg_vacantes";

        private static Vacante Mapear(OracleDataReader r)
        {
            return new Vacante
            {
                Id = Int(r, "id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                Emprendimiento = Txt(r, "emprendimiento"),
                CargoId = Int(r, "cargo_id"),
                Cargo = Txt(r, "cargo"),
                SalarioBase = Dec(r, "salario_base"),
                Titulo = Txt(r, "titulo"),
                Descripcion = Txt(r, "descripcion"),
                CiudadCodigo = Txt(r, "ciudad_codigo"),
                Ciudad = Txt(r, "ciudad"),
                FechaPublicacion = Fec(r, "fecha_publicacion"),
                FechaCierre = FecNula(r, "fecha_cierre"),
                Estado = Txt(r, "estado"),
                Postulaciones = Int(r, "postulaciones")
            };
        }

        private static void Parametros(OracleCommand c, Vacante v)
        {
            Entero(c, "cargo", v.CargoId);
            Texto(c, "titulo", v.Titulo);
            Texto(c, "descripcion", v.Descripcion);
            Texto(c, "ciudad", v.CiudadCodigo);
            Fecha(c, "cierre", v.FechaCierre);
        }

        public List<Vacante> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp ORDER BY estado, fecha_publicacion DESC", Mapear,
                         c => Entero(c, "emp", emprendimientoId));
        }

        public List<Vacante> ListarAbiertas()
        {
            return Lista(Sql + " WHERE estado = 'abierta' ORDER BY fecha_publicacion DESC", Mapear);
        }

        public Vacante? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Vacante Insertar(Vacante v)
        {
            v.Id = InsertarConId(
                "INSERT INTO sg_vacante (emprendimiento_id, cargo_id, titulo, descripcion, ciudad_codigo, fecha_cierre) " +
                "VALUES (:p_emp, :p_cargo, :p_titulo, :p_descripcion, :p_ciudad, :p_cierre)",
                c => { Entero(c, "emp", v.EmprendimientoId); Parametros(c, v); });
            return v;
        }

        public void Actualizar(Vacante v)
        {
            EjecutarSql("UPDATE sg_vacante SET cargo_id = :p_cargo, titulo = :p_titulo, descripcion = :p_descripcion, " +
                        "ciudad_codigo = :p_ciudad, fecha_cierre = :p_cierre WHERE id = :p_id",
                c => { Parametros(c, v); Entero(c, "id", v.Id); });
        }

        public void CambiarEstado(int id, string estado)
        {
            EjecutarSql("UPDATE sg_vacante SET estado = :p_estado WHERE id = :p_id",
                c => { Texto(c, "estado", estado); Entero(c, "id", id); });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_vacante WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }
    }

    public class PostulacionDaoOracle : IPostulacionDao
    {
        private const string Sql =
            "SELECT id, vacante_id, emprendimiento_id, vacante, tipo_documento, numero_documento, nombre, apellido, telefono, " +
            "email, ciudad_codigo, ciudad, cv_nombre, cv_tipo, cv_bytes, fecha, estado FROM vw_sg_postulaciones";

        private static Postulacion Mapear(OracleDataReader r)
        {
            return new Postulacion
            {
                Id = Int(r, "id"),
                VacanteId = Int(r, "vacante_id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                Vacante = Txt(r, "vacante"),
                TipoDocumento = Txt(r, "tipo_documento"),
                NumeroDocumento = Txt(r, "numero_documento"),
                Nombre = Txt(r, "nombre"),
                Apellido = Txt(r, "apellido"),
                Telefono = Txt(r, "telefono"),
                Email = Txt(r, "email"),
                CiudadCodigo = Txt(r, "ciudad_codigo"),
                Ciudad = Txt(r, "ciudad"),
                CvNombre = Txt(r, "cv_nombre"),
                CvTipo = Txt(r, "cv_tipo"),
                CvBytes = LongNulo(r, "cv_bytes") ?? 0,
                Fecha = Fec(r, "fecha"),
                Estado = Txt(r, "estado")
            };
        }

        public List<Postulacion> ListarPorVacante(int vacanteId)
        {
            return Lista(Sql + " WHERE vacante_id = :p_vacante ORDER BY fecha DESC", Mapear, c => Entero(c, "vacante", vacanteId));
        }

        public Postulacion? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public int Insertar(Postulacion p, ArchivoCv cv)
        {
            // autoriza_datos = 'S': solo se llega aqui si el aspirante acepto el tratamiento de datos (Ley 1581 de 2012).
            return InsertarConId(
                "INSERT INTO sg_postulacion (vacante_id, tipo_documento, numero_documento, nombre, apellido, telefono, email, " +
                "ciudad_codigo, cv, cv_nombre, cv_tipo, telegram_chat_id, autoriza_datos) VALUES (:p_vacante, :p_tipo, :p_numero, " +
                ":p_nombre, :p_apellido, :p_telefono, :p_email, :p_ciudad, :p_cv, :p_cv_nombre, :p_cv_tipo, :p_chat, 'S')",
                c =>
                {
                    Entero(c, "vacante", p.VacanteId);
                    Texto(c, "tipo", p.TipoDocumento);
                    Texto(c, "numero", p.NumeroDocumento);
                    Texto(c, "nombre", p.Nombre);
                    Texto(c, "apellido", p.Apellido);
                    Texto(c, "telefono", p.Telefono);
                    Texto(c, "email", p.Email);
                    Texto(c, "ciudad", p.CiudadCodigo);
                    Param(c, "cv", OracleDbType.Blob, cv.Contenido);
                    Texto(c, "cv_nombre", cv.Nombre);
                    Texto(c, "cv_tipo", cv.Tipo);
                    Largo(c, "chat", p.TelegramChatId);
                });
        }

        public ArchivoCv? ObtenerCv(int postulacionId)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "SELECT cv, cv_nombre, cv_tipo FROM sg_postulacion WHERE id = :p_id"))
                {
                    Entero(cmd, "id", postulacionId);
                    using (OracleDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;
                        OracleBlob blob = r.GetOracleBlob(r.GetOrdinal("cv"));
                        return new ArchivoCv
                        {
                            Contenido = blob.IsNull ? Array.Empty<byte>() : blob.Value,
                            Nombre = Txt(r, "cv_nombre"),
                            Tipo = Txt(r, "cv_tipo")
                        };
                    }
                }
            });
        }

        public void CambiarEstado(int id, string estado)
        {
            EjecutarSql("UPDATE sg_postulacion SET estado = :p_estado WHERE id = :p_id",
                c => { Texto(c, "estado", estado); Entero(c, "id", id); });
        }

        public int Contratar(int postulacionId, DateTime fechaIngreso)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_contratar_postulante(:p_post, :p_fecha, :p_trab); END;"))
                {
                    Entero(cmd, "post", postulacionId);
                    Fecha(cmd, "fecha", fechaIngreso);
                    OracleParameter trab = Param(cmd, "trab", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(trab);
                }
            });
        }
    }

    /// <summary>Todos los numeros los calcula Oracle: paquetes pkg_sg_nomina y pkg_sg_reportes y procedimientos sp_sg_*.</summary>
    public class ReporteDaoOracle : IReporteDao
    {
        private static ResumenEmprendimiento MapearResumen(OracleDataReader r, bool completo)
        {
            ResumenEmprendimiento x = new ResumenEmprendimiento
            {
                EmprendimientoId = Int(r, "emprendimiento_id"),
                Nombre = Txt(r, "nombre"),
                Sector = Txt(r, "sector"),
                TrabajadoresActivos = Int(r, "trabajadores_activos"),
                Productos = Int(r, "productos"),
                ProductosStockBajo = Int(r, "productos_stock_bajo"),
                VentasDelMes = Int(r, "ventas_del_mes"),
                PostulacionesNuevas = Int(r, "postulaciones_nuevas"),
                Balance = Dec(r, "balance")
            };
            if (completo)
            {
                x.Ingresos = Dec(r, "ingresos");
                x.Gastos = Dec(r, "gastos");
                x.NominaMensual = Dec(r, "nomina_mensual");
            }
            return x;
        }

        public ResumenEmprendimiento? Resumen(int emprendimientoId)
        {
            List<ResumenEmprendimiento> filas = ListaCursor("BEGIN sp_sg_resumen_emprendimiento(:p_emp, :p_resultado); END;",
                r => MapearResumen(r, true), c => Entero(c, "emp", emprendimientoId));
            return filas.Count > 0 ? filas[0] : null;
        }

        public List<ResumenEmprendimiento> ResumenesDelUsuario(int usuarioId)
        {
            return Lista("SELECT * FROM vw_sg_resumen_emprendimiento WHERE usuario_id = :p_usuario ORDER BY nombre",
                r => MapearResumen(r, false), c => Entero(c, "usuario", usuarioId));
        }

        private static decimal Funcion(string funcion, int emprendimientoId)
        {
            return Valor("SELECT " + funcion + "(:p_emp) FROM dual", c => Entero(c, "emp", emprendimientoId));
        }

        public ResumenNomina Nomina(int emprendimientoId)
        {
            return Ejecutar(con =>
            {
                // Una sola ida a la base de datos para los cinco valores del paquete.
                using (OracleCommand cmd = Comando(con,
                    "SELECT pkg_sg_nomina.trabajadores_activos(:p_emp) AS activos, pkg_sg_nomina.nomina_mensual(:p_emp) AS mensual, " +
                    "pkg_sg_nomina.nomina_quincenal(:p_emp) AS quincenal, pkg_sg_nomina.total_deducciones(:p_emp) AS deducciones, " +
                    "pkg_sg_nomina.nomina_neta(:p_emp) AS neta FROM dual"))
                {
                    Entero(cmd, "emp", emprendimientoId);
                    using (OracleDataReader r = cmd.ExecuteReader())
                    {
                        r.Read();
                        return new ResumenNomina
                        {
                            TrabajadoresActivos = Int(r, "activos"),
                            NominaMensual = Dec(r, "mensual"),
                            NominaQuincenal = Dec(r, "quincenal"),
                            TotalDeducciones = Dec(r, "deducciones"),
                            NominaNeta = Dec(r, "neta")
                        };
                    }
                }
            });
        }

        public List<FilaNomina> DetalleNomina(int emprendimientoId)
        {
            return ListaCursor("BEGIN pkg_sg_nomina.listar_nomina(:p_emp, :p_resultado); END;",
                r => new FilaNomina
                {
                    TrabajadorId = Int(r, "trabajador_id"),
                    TipoDocumento = Txt(r, "tipo_documento"),
                    NumeroDocumento = Txt(r, "numero_documento"),
                    Trabajador = Txt(r, "trabajador"),
                    Cargo = Txt(r, "cargo"),
                    SalarioBase = Dec(r, "salario_base"),
                    PagoQuincenal = Dec(r, "pago_quincenal"),
                    Salud = Dec(r, "salud"),
                    Pension = Dec(r, "pension"),
                    Deducciones = Dec(r, "deducciones"),
                    Neto = Dec(r, "neto")
                },
                c => Entero(c, "emp", emprendimientoId));
        }

        public ResumenFinanciero Finanzas(int emprendimientoId)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con,
                    "SELECT pkg_sg_reportes.total_ingresos(:p_emp) AS ingresos, pkg_sg_reportes.total_gastos(:p_emp) AS gastos, " +
                    "pkg_sg_reportes.utilidad(:p_emp) AS utilidad FROM dual"))
                {
                    Entero(cmd, "emp", emprendimientoId);
                    using (OracleDataReader r = cmd.ExecuteReader())
                    {
                        r.Read();
                        return new ResumenFinanciero
                        {
                            TotalIngresos = Dec(r, "ingresos"),
                            TotalGastos = Dec(r, "gastos"),
                            Balance = Dec(r, "utilidad")
                        };
                    }
                }
            });
        }

        public decimal VentasEntre(int emprendimientoId, DateTime desde, DateTime hasta)
        {
            return Valor("SELECT pkg_sg_reportes.total_ventas_rango(:p_emp, :p_desde, :p_hasta) FROM dual",
                c => { Entero(c, "emp", emprendimientoId); Fecha(c, "desde", desde); Fecha(c, "hasta", hasta); });
        }

        public decimal ValorInventario(int emprendimientoId)
        {
            return Funcion("pkg_sg_reportes.valor_inventario", emprendimientoId);
        }

        public List<ValorEtiquetado> TopProductos(int emprendimientoId, int limite)
        {
            return ListaCursor("BEGIN pkg_sg_reportes.top_productos(:p_emp, :p_limite, :p_resultado); END;",
                r => new ValorEtiquetado { Etiqueta = Txt(r, "nombre"), Cantidad = Int(r, "unidades"), Valor = Dec(r, "ingresos") },
                c => { Entero(c, "emp", emprendimientoId); Entero(c, "limite", limite); });
        }

        public List<ValorEtiquetado> VentasPorMes(int emprendimientoId, int meses)
        {
            return ListaCursor("BEGIN pkg_sg_reportes.ventas_por_mes(:p_emp, :p_meses, :p_resultado); END;",
                r => new ValorEtiquetado { Etiqueta = Txt(r, "mes"), Valor = Dec(r, "total") },
                c => { Entero(c, "emp", emprendimientoId); Entero(c, "meses", meses); });
        }

        public List<ValorEtiquetado> GastosPorCategoria(int emprendimientoId, string? mes)
        {
            return ListaCursor("BEGIN pkg_sg_reportes.gastos_por_categoria(:p_emp, :p_mes, :p_resultado); END;",
                r => new ValorEtiquetado { Etiqueta = Txt(r, "categoria"), Valor = Dec(r, "total") },
                c => { Entero(c, "emp", emprendimientoId); Texto(c, "mes", mes); });
        }

        public int LimpiarBitacora(int dias)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_limpiar_bitacora(:p_dias, :p_eliminados); END;"))
                {
                    Entero(cmd, "dias", dias);
                    OracleParameter eliminados = Param(cmd, "eliminados", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(eliminados);
                }
            });
        }

        public int CerrarVacantesVencidas()
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN sp_sg_cerrar_vacantes_vencidas(:p_cerradas); END;"))
                {
                    OracleParameter cerradas = Param(cmd, "cerradas", OracleDbType.Int32, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return SalidaEntero(cerradas);
                }
            });
        }
    }

    public class TelegramDaoOracle : ITelegramDao
    {
        public string GenerarCodigo(int usuarioId)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN :p_codigo := pkg_sg_telegram.generar_codigo(:p_usuario); END;"))
                {
                    OracleParameter codigo = Param(cmd, "codigo", OracleDbType.Varchar2, null, ParameterDirection.Output);
                    Entero(cmd, "usuario", usuarioId);
                    cmd.ExecuteNonQuery();
                    return SalidaTexto(codigo);
                }
            });
        }

        public VinculoTelegram Vincular(long chatId, string codigo)
        {
            return Ejecutar(con =>
            {
                using (OracleCommand cmd = Comando(con, "BEGIN pkg_sg_telegram.vincular(:p_chat, :p_codigo, :p_usuario, :p_nombre); END;"))
                {
                    Largo(cmd, "chat", chatId);
                    Texto(cmd, "codigo", codigo);
                    OracleParameter usuario = Param(cmd, "usuario", OracleDbType.Int32, null, ParameterDirection.Output);
                    OracleParameter nombre = Param(cmd, "nombre", OracleDbType.Varchar2, null, ParameterDirection.Output);
                    cmd.ExecuteNonQuery();
                    return new VinculoTelegram { UsuarioId = SalidaEntero(usuario), NombreUsuario = SalidaTexto(nombre) };
                }
            });
        }

        private static int? FuncionDeChat(string funcion, long chatId)
        {
            decimal valor = Valor("SELECT NVL(" + funcion + "(:p_chat), 0) FROM dual", c => Largo(c, "chat", chatId));
            return valor == 0m ? (int?)null : (int)valor;
        }

        public int? UsuarioDeChat(long chatId) { return FuncionDeChat("pkg_sg_telegram.usuario_de_chat", chatId); }
        public int? EmprendimientoDeChat(long chatId) { return FuncionDeChat("pkg_sg_telegram.emprendimiento_de_chat", chatId); }

        public void FijarEmprendimiento(long chatId, int emprendimientoId)
        {
            EjecutarSql("BEGIN pkg_sg_telegram.fijar_emprendimiento(:p_chat, :p_emp); END;",
                c => { Largo(c, "chat", chatId); Entero(c, "emp", emprendimientoId); });
        }

        public void Desvincular(long chatId)
        {
            EjecutarSql("BEGIN pkg_sg_telegram.desvincular(:p_chat); END;", c => Largo(c, "chat", chatId));
        }

        public int ChatsVinculados(int usuarioId)
        {
            return (int)Valor("SELECT pkg_sg_telegram.chats_vinculados(:p_usuario) FROM dual", c => Entero(c, "usuario", usuarioId));
        }
    }
}
