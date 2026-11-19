using System;
using System.Collections.Generic;
using Datos;
using Entidades;
using Logica.Pdf;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: reunir los indicadores (todos calculados por Oracle) y generar los PDF.
    /// Los PDF se devuelven como bytes: la app los guarda donde el usuario elija y el bot los envia por Telegram.
    /// </summary>
    public class ServicioReportes
    {
        private readonly IReporteDao _reportes;
        private readonly ServicioVentas _ventas;
        private readonly ServicioInventario _inventario;
        private readonly ServicioPresupuestos _presupuestos;
        private readonly ServicioPlanes _planes;
        private readonly ServicioFinanzas _finanzas;

        public ServicioReportes(IReporteDao reportes, ServicioVentas ventas, ServicioInventario inventario,
                                ServicioPresupuestos presupuestos, ServicioPlanes planes, ServicioFinanzas finanzas)
        {
            _planes = planes;
            _reportes = reportes;
            _ventas = ventas;
            _inventario = inventario;
            _presupuestos = presupuestos;
            _finanzas = finanzas;
        }

        /// <summary>
        /// Indicadores y graficas del Inicio (plan Gratis tambien). Las sumas grandes las hace Oracle
        /// (resumen, top productos, gastos por categoria); los agrupados por dia y mes salen de las listas.
        /// </summary>
        public DatosTablero Tablero(int emprendimientoId)
        {
            DateTime hoy = DateTime.Now;
            List<Producto> productos = _inventario.Listar(emprendimientoId);
            List<Presupuesto> enAlerta = new List<Presupuesto>();
            foreach (Presupuesto p in _presupuestos.Listar(emprendimientoId, Validar.MesActual()))
                if (p.Estado != Presupuesto.EstadoOk) enAlerta.Add(p);

            return new DatosTablero
            {
                Resumen = Resumen(emprendimientoId),
                Ventas = _ventas.Resumen(emprendimientoId),
                VentasPorDia = Estadisticas.VentasPorDia(_ventas.Listar(emprendimientoId), hoy, 14),
                IngresosGastos = Estadisticas.IngresosGastosPorMes(_finanzas.Listar(emprendimientoId), hoy, 6),
                GastosPorCategoria = Estadisticas.Principales(_reportes.GastosPorCategoria(emprendimientoId, null), 6),
                TopProductos = _reportes.TopProductos(emprendimientoId, 5),
                EstadoInventario = Estadisticas.EstadoInventario(productos),
                StockBajo = _inventario.StockBajo(emprendimientoId),
                PresupuestosEnAlerta = enAlerta
            };
        }

        public ResumenEmprendimiento Resumen(int emprendimientoId)
        {
            return _reportes.Resumen(emprendimientoId) ?? throw new ReglaDeNegocioException("Emprendimiento no encontrado.");
        }

        /// <summary>pkg_sg_reportes.total_ventas_rango.</summary>
        public decimal VentasEntre(int emprendimientoId, DateTime desde, DateTime hasta)
        {
            return _reportes.VentasEntre(emprendimientoId, desde.Date, hasta.Date);
        }

        /// <summary>pkg_sg_reportes.top_productos.</summary>
        public System.Collections.Generic.List<ValorEtiquetado> TopProductos(int emprendimientoId, int limite)
        {
            return _reportes.TopProductos(emprendimientoId, Math.Clamp(limite, 1, 20));
        }

        /// <summary>pkg_sg_reportes.gastos_por_categoria (mes null = todo el historial).</summary>
        public System.Collections.Generic.List<ValorEtiquetado> GastosPorCategoria(int emprendimientoId, string? mes)
        {
            return _reportes.GastosPorCategoria(emprendimientoId, mes == null ? null : Validar.Mes(mes));
        }

        public DatosReporte Reunir(Emprendimiento emprendimiento)
        {
            int id = emprendimiento.Id;
            return new DatosReporte
            {
                Emprendimiento = emprendimiento,
                Generado = DateTime.Now,
                Resumen = Resumen(id),
                Ventas = _ventas.Resumen(id),
                Finanzas = _reportes.Finanzas(id),
                Nomina = _reportes.Nomina(id),
                ValorInventario = _reportes.ValorInventario(id),
                VentasPorMes = _reportes.VentasPorMes(id, 6),
                TopProductos = _reportes.TopProductos(id, 8),
                GastosPorCategoria = _reportes.GastosPorCategoria(id, null),
                StockBajo = _inventario.StockBajo(id),
                Presupuestos = _presupuestos.Listar(id, Validar.MesActual()),
                DetalleNomina = _reportes.DetalleNomina(id)
            };
        }

        public byte[] ReportePdf(Emprendimiento emprendimiento)
        {
            _planes.ExigirPro(emprendimiento.UsuarioId, "El reporte en PDF");
            return DocumentoReporte.Generar(Reunir(emprendimiento));
        }

        public byte[] FacturaPdf(Emprendimiento emprendimiento, Venta venta)
        {
            if (venta.Detalles.Count == 0)
                throw new ReglaDeNegocioException("La venta no tiene productos.");
            return DocumentoFactura.Generar(emprendimiento, venta);
        }

        public static string NombreArchivoReporte(Emprendimiento e)
        {
            return "Reporte_" + Limpiar(e.Nombre) + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".pdf";
        }

        public static string NombreArchivoFactura(Venta v)
        {
            return "Factura_" + v.Id.ToString("00000") + ".pdf";
        }

        private static string Limpiar(string texto)
        {
            char[] invalidos = System.IO.Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in texto) sb.Append(Array.IndexOf(invalidos, c) >= 0 || c == ' ' ? '_' : c);
            return sb.ToString();
        }
    }
}
