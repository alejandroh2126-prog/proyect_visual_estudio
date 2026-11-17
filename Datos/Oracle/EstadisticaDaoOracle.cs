using System;
using System.Data;
using System.Data.Common;

namespace Datos.Oracle
{
    /// <summary>
    /// UNA SOLA MISION: pedirle a Oracle numeros ya calculados llamando a sus funciones y paquetes PL/SQL
    /// (SELECT funcion(:parametro) FROM dual) y ejecutar su procedimiento de mantenimiento.
    /// Los nombres de funciones son constantes de este archivo; los valores siempre van como parametros.
    /// </summary>
    public class EstadisticaDaoOracle : IEstadisticaDao
    {
        public int TrabajadoresActivos(int emprendimientoId)
        {
            return (int)Numero("pkg_sg_nomina.trabajadores_activos", emprendimientoId);
        }

        public decimal NominaMensual(int emprendimientoId) { return Numero("pkg_sg_nomina.nomina_mensual", emprendimientoId); }
        public decimal NominaQuincenal(int emprendimientoId) { return Numero("pkg_sg_nomina.nomina_quincenal", emprendimientoId); }
        public decimal TotalDeducciones(int emprendimientoId) { return Numero("pkg_sg_nomina.total_deducciones", emprendimientoId); }
        public decimal NominaNeta(int emprendimientoId) { return Numero("pkg_sg_nomina.nomina_neta", emprendimientoId); }
        public decimal TotalIngresos(int emprendimientoId) { return Numero("pkg_sg_reportes.total_ingresos", emprendimientoId); }
        public decimal TotalGastos(int emprendimientoId) { return Numero("pkg_sg_reportes.total_gastos", emprendimientoId); }

        public void LimpiarBitacora(int dias)
        {
            ConexionOracle.Ejecutar(con =>
            {
                using (DbCommand cmd = ConexionOracle.Comando(con, "BEGIN sp_sg_limpiar_bitacora(:p_dias); END;"))
                {
                    ConexionOracle.Param(cmd, "dias", DbType.Int32, dias);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        private static decimal Numero(string funcion, int emprendimientoId)
        {
            return ConexionOracle.Ejecutar(con =>
            {
                using (DbCommand cmd = ConexionOracle.Comando(con, "SELECT " + funcion + "(:p_emprendimiento) FROM dual"))
                {
                    ConexionOracle.Param(cmd, "emprendimiento", DbType.Int32, emprendimientoId);
                    object? valor = cmd.ExecuteScalar();
                    if (valor == null || valor == DBNull.Value) return 0m;
                    return Convert.ToDecimal(valor);
                }
            });
        }
    }
}
