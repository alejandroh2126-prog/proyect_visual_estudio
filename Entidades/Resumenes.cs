using System;
using System.Collections.Generic;

namespace Entidades
{
    /// <summary>
    /// Error "esperado" del negocio (dato invalido, stock insuficiente, regla de Oracle...).
    /// La interfaz y el bot muestran su mensaje tal cual.
    /// </summary>
    public class ReglaDeNegocioException : Exception
    {
        public ReglaDeNegocioException(string mensaje) : base(mensaje)
        {
        }
    }

    /// <summary>Resumen de un emprendimiento (sp_sg_resumen_emprendimiento / vw_sg_resumen_emprendimiento).</summary>
    public class ResumenEmprendimiento
    {
        public int EmprendimientoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public int TrabajadoresActivos { get; set; }
        public int Productos { get; set; }
        public int ProductosStockBajo { get; set; }
        public int VentasDelMes { get; set; }
        public int PostulacionesNuevas { get; set; }
        public decimal Balance { get; set; }
        public decimal Ingresos { get; set; }
        public decimal Gastos { get; set; }
        public decimal NominaMensual { get; set; }
    }

    /// <summary>Totales de nomina (paquete pkg_sg_nomina).</summary>
    public class ResumenNomina
    {
        public int TrabajadoresActivos { get; set; }
        public decimal NominaMensual { get; set; }
        public decimal NominaQuincenal { get; set; }
        public decimal TotalDeducciones { get; set; }
        public decimal NominaNeta { get; set; }
    }

    /// <summary>Una fila de la nomina (pkg_sg_nomina.listar_nomina).</summary>
    public class FilaNomina
    {
        public int TrabajadorId { get; set; }
        public string TipoDocumento { get; set; } = string.Empty;
        public string NumeroDocumento { get; set; } = string.Empty;
        public string Trabajador { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public decimal PagoQuincenal { get; set; }
        public decimal Salud { get; set; }
        public decimal Pension { get; set; }
        public decimal Deducciones { get; set; }
        public decimal Neto { get; set; }
    }

    /// <summary>Totales financieros (pkg_sg_reportes).</summary>
    public class ResumenFinanciero
    {
        public decimal TotalIngresos { get; set; }
        public decimal TotalGastos { get; set; }

        /// <summary>pkg_sg_reportes.utilidad (usa fn_sg_balance).</summary>
        public decimal Balance { get; set; }
    }

    /// <summary>Ventas por periodo (pkg_sg_reportes.total_ventas_rango).</summary>
    public class ResumenVentas
    {
        public decimal Hoy { get; set; }
        public decimal UltimosSieteDias { get; set; }
        public decimal EsteMes { get; set; }
        public decimal TotalHistorico { get; set; }
        public int CantidadVentas { get; set; }
    }

    /// <summary>Par etiqueta/valor para graficas y tablas (ventas por mes, gastos por categoria, top productos).</summary>
    public class ValorEtiquetado
    {
        public string Etiqueta { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>Producto con stock bajo (sp_sg_productos_stock_bajo).</summary>
    public class ProductoStockBajo
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public int Faltante { get; set; }
    }

    /// <summary>Todo lo que lleva el reporte PDF.</summary>
    public class DatosReporte
    {
        public Emprendimiento Emprendimiento { get; set; } = new Emprendimiento();
        public DateTime Generado { get; set; } = DateTime.Now;
        public ResumenEmprendimiento Resumen { get; set; } = new ResumenEmprendimiento();
        public ResumenVentas Ventas { get; set; } = new ResumenVentas();
        public ResumenFinanciero Finanzas { get; set; } = new ResumenFinanciero();
        public ResumenNomina Nomina { get; set; } = new ResumenNomina();
        public decimal ValorInventario { get; set; }
        public List<ValorEtiquetado> VentasPorMes { get; set; } = new List<ValorEtiquetado>();
        public List<ValorEtiquetado> TopProductos { get; set; } = new List<ValorEtiquetado>();
        public List<ValorEtiquetado> GastosPorCategoria { get; set; } = new List<ValorEtiquetado>();
        public List<ProductoStockBajo> StockBajo { get; set; } = new List<ProductoStockBajo>();
        public List<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();
        public List<FilaNomina> DetalleNomina { get; set; } = new List<FilaNomina>();
    }

    /// <summary>Elemento de una sugerencia de pregunta frecuente (tabla sg_conocimiento).</summary>
    public class PreguntaFrecuente
    {
        public string Tema { get; set; } = string.Empty;
        public string Pregunta { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
    }

    /// <summary>Resultado de vincular un chat de Telegram.</summary>
    public class VinculoTelegram
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
    }
}
