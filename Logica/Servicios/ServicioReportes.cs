using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Entidades;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: estadisticas y reporte HTML con graficas.
    /// Mejora sobre Java: los nombres se escapan (no se puede "inyectar" HTML)
    /// y el reporte incluye tablas por si no hay internet para cargar Chart.js.
    /// </summary>
    public class ServicioReportes
    {
        private readonly ServicioVentas _ventas;
        private readonly ServicioInventario _inventario;
        private readonly ServicioFinanzas _finanzas;

        public ServicioReportes(ServicioVentas ventas, ServicioInventario inventario, ServicioFinanzas finanzas)
        {
            _ventas = ventas;
            _inventario = inventario;
            _finanzas = finanzas;
        }

        public ResumenVentas CalcularResumenVentas(int emprendimientoId)
        {
            List<Venta> ventas = _ventas.Listar(emprendimientoId);
            DateTime hoy = DateTime.Today;
            DateTime hace7 = hoy.AddDays(-6);
            DateTime inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            ResumenVentas r = new ResumenVentas { CantidadVentas = ventas.Count };
            Dictionary<string, int> conteo = new Dictionary<string, int>();

            foreach (Venta v in ventas)
            {
                decimal total = v.Total;
                r.TotalHistorico += total;
                if (v.Fecha.Date == hoy) r.Hoy += total;
                if (v.Fecha.Date >= hace7) r.UltimosSieteDias += total;
                if (v.Fecha.Date >= inicioMes) r.EsteMes += total;

                foreach (DetalleVenta d in v.Detalles)
                {
                    if (conteo.ContainsKey(d.ProductoNombre)) conteo[d.ProductoNombre] += d.Cantidad;
                    else conteo[d.ProductoNombre] = d.Cantidad;
                }
            }

            List<KeyValuePair<string, int>> orden = new List<KeyValuePair<string, int>>(conteo);
            orden.Sort((a, b) => b.Value.CompareTo(a.Value));
            r.TopProductos = orden.Count > 8 ? orden.GetRange(0, 8) : orden;
            return r;
        }

        public string GenerarReporteHtml(Emprendimiento emp)
        {
            List<Venta> ventas = _ventas.Listar(emp.Id);
            List<Producto> productos = _inventario.Listar(emp.Id);
            ResumenVentas resumen = CalcularResumenVentas(emp.Id);
            ResumenFinanciero fin = _finanzas.CalcularResumen(emp.Id);

            SortedDictionary<string, decimal> porMes = new SortedDictionary<string, decimal>();
            foreach (Venta v in ventas)
            {
                string mes = v.Fecha.ToString("yyyy-MM");
                if (porMes.ContainsKey(mes)) porMes[mes] += v.Total; else porMes[mes] = v.Total;
            }

            List<Producto> porStock = new List<Producto>(productos);
            porStock.Sort((a, b) => b.Stock.CompareTo(a.Stock));
            if (porStock.Count > 8) porStock = porStock.GetRange(0, 8);

            List<string> labVend = new List<string>(); List<int> datVend = new List<int>();
            foreach (KeyValuePair<string, int> kv in resumen.TopProductos) { labVend.Add(kv.Key); datVend.Add(kv.Value); }

            List<string> labStock = new List<string>(); List<int> datStock = new List<int>();
            foreach (Producto p in porStock) { labStock.Add(p.Nombre); datStock.Add(p.Stock); }

            List<string> labMes = new List<string>(porMes.Keys);
            List<decimal> datMes = new List<decimal>(porMes.Values);

            string e(string? s) { return WebUtility.HtmlEncode(s ?? string.Empty); }
            string js(object o) { return JsonSerializer.Serialize(o).Replace("<", "\\u003c"); }

            StringBuilder h = new StringBuilder();
            h.AppendLine("<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'/>");
            h.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'/>");
            h.AppendLine("<title>Reporte SGAPE - " + e(emp.Nombre) + "</title>");
            h.AppendLine("<script src='https://cdn.jsdelivr.net/npm/chart.js'></script>");
            h.AppendLine("<style>");
            h.AppendLine("*{margin:0;padding:0;box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;background:#f0f4f8;color:#1a1a18}");
            h.AppendLine(".header{background:linear-gradient(135deg,#085041,#1D9E75);color:#fff;padding:32px 40px}.header h1{font-size:26px;margin-bottom:6px}.header p{opacity:.85;font-size:14px}");
            h.AppendLine(".container{max-width:1100px;margin:0 auto;padding:28px 20px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin-bottom:20px}.grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin-bottom:20px}");
            h.AppendLine(".card{background:#fff;border-radius:14px;padding:22px;box-shadow:0 4px 18px rgba(0,0,0,.08)}.card h3{font-size:15px;color:#085041;margin-bottom:14px}");
            h.AppendLine(".num{font-size:28px;font-weight:800;color:#1D9E75}.red .num{color:#E24B4A}.gold .num{color:#BA7517}.lbl{font-size:13px;color:#777}");
            h.AppendLine(".alerta{background:#FAEEDA;border-left:4px solid #BA7517;border-radius:8px;padding:10px 14px;margin-bottom:8px;font-size:13px}");
            h.AppendLine("table{width:100%;border-collapse:collapse;font-size:13px}td,th{padding:6px 8px;border-bottom:1px solid #eee;text-align:left}canvas{max-height:260px}");
            h.AppendLine(".footer{text-align:center;padding:24px;color:#999;font-size:12px}@media(max-width:700px){.grid,.grid3{grid-template-columns:1fr}}");
            h.AppendLine("</style></head><body>");
            h.AppendLine("<div class='header'><h1>Reporte estadístico - " + e(emp.Nombre) + "</h1>");
            h.AppendLine("<p>Generado el " + Formato.FechaHora(DateTime.Now) + " | Sector: " + e(emp.Sector) + " | Estado: " + e(emp.Estado) + "</p></div>");
            h.AppendLine("<div class='container'>");

            h.AppendLine("<div class='grid3'>");
            h.AppendLine("<div class='card'><div class='num'>" + e(Formato.Moneda(resumen.TotalHistorico)) + "</div><div class='lbl'>Total ventas (" + resumen.CantidadVentas + ")</div></div>");
            h.AppendLine("<div class='card red'><div class='num'>" + e(Formato.Moneda(fin.TotalGastos)) + "</div><div class='lbl'>Total gastos</div></div>");
            h.AppendLine("<div class='card gold'><div class='num'>" + e(Formato.Moneda(fin.Balance)) + "</div><div class='lbl'>Balance</div></div>");
            h.AppendLine("</div>");

            List<Producto> bajos = productos.FindAll(p => p.TieneStockBajo);
            if (bajos.Count > 0)
            {
                h.AppendLine("<div class='card' style='margin-bottom:20px'><h3>Alertas de stock bajo</h3>");
                foreach (Producto p in bajos)
                    h.AppendLine("<div class='alerta'><strong>" + e(p.Nombre) + "</strong> - stock: " + p.Stock + " (mínimo " + p.StockMinimo + ")</div>");
                h.AppendLine("</div>");
            }

            h.AppendLine("<div class='grid'><div class='card'><h3>Productos más vendidos</h3><canvas id='cVend'></canvas></div>");
            h.AppendLine("<div class='card'><h3>Inventario actual</h3><canvas id='cStock'></canvas></div></div>");
            h.AppendLine("<div class='grid'><div class='card'><h3>Ventas por mes</h3><canvas id='cMes'></canvas></div>");
            h.AppendLine("<div class='card'><h3>Ingresos vs gastos</h3><canvas id='cDon'></canvas></div></div>");

            h.AppendLine("<div class='card'><h3>Resumen por período</h3><table>");
            h.AppendLine("<tr><th>Hoy</th><td>" + e(Formato.Moneda(resumen.Hoy)) + "</td></tr>");
            h.AppendLine("<tr><th>Últimos 7 días</th><td>" + e(Formato.Moneda(resumen.UltimosSieteDias)) + "</td></tr>");
            h.AppendLine("<tr><th>Este mes</th><td>" + e(Formato.Moneda(resumen.EsteMes)) + "</td></tr></table></div>");

            h.AppendLine("<script>");
            h.AppendLine("if(typeof Chart!=='undefined'){");
            h.AppendLine("const col=['#1D9E75','#085041','#5DCAA5','#BA7517','#E24B4A','#3730A3','#F59E0B','#64748b'];");
            h.AppendLine("new Chart(cVend,{type:'bar',data:{labels:" + js(labVend) + ",datasets:[{label:'Unidades',data:" + js(datVend) + ",backgroundColor:col,borderRadius:6}]},options:{plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});");
            h.AppendLine("new Chart(cStock,{type:'bar',data:{labels:" + js(labStock) + ",datasets:[{label:'Stock',data:" + js(datStock) + ",backgroundColor:'#1D9E75',borderRadius:6}]},options:{indexAxis:'y',plugins:{legend:{display:false}},scales:{x:{beginAtZero:true}}}});");
            h.AppendLine("new Chart(cMes,{type:'line',data:{labels:" + js(labMes) + ",datasets:[{label:'Ventas',data:" + js(datMes) + ",borderColor:'#1D9E75',backgroundColor:'rgba(29,158,117,.12)',fill:true,tension:.4}]},options:{scales:{y:{beginAtZero:true}}}});");
            h.AppendLine("new Chart(cDon,{type:'doughnut',data:{labels:['Ingresos','Gastos'],datasets:[{data:[" + fin.TotalIngresos.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + fin.TotalGastos.ToString(System.Globalization.CultureInfo.InvariantCulture) + "],backgroundColor:['#1D9E75','#E24B4A'],borderWidth:0}]},options:{plugins:{legend:{position:'bottom'}},cutout:'65%'}});");
            h.AppendLine("}</script>");
            h.AppendLine("</div><div class='footer'>SGAPE - Universidad Popular del Cesar - " + DateTime.Now.Year + "</div></body></html>");

            string ruta = Path.Combine(RutasApp.CarpetaReportes,
                "reporte_" + emp.Id + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html");
            File.WriteAllText(ruta, h.ToString(), new UTF8Encoding(false));
            return ruta;
        }
    }
}