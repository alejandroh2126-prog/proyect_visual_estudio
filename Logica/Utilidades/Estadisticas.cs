using System;
using System.Collections.Generic;
using System.Globalization;
using Entidades;

namespace Logica.Utilidades
{
    /// <summary>
    /// Agrupa ventas, movimientos y productos para las graficas del tablero.
    /// Funciones puras (sin base de datos): reciben las listas y la fecha de hoy, asi se pueden probar facil.
    /// </summary>
    public static class Estadisticas
    {
        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

        /// <summary>Total vendido cada dia de los ultimos <paramref name="dias"/> dias (incluye hoy y los dias sin ventas).</summary>
        public static List<ValorEtiquetado> VentasPorDia(List<Venta> ventas, DateTime hoy, int dias)
        {
            dias = Math.Clamp(dias, 1, 60);
            DateTime desde = hoy.Date.AddDays(-(dias - 1));
            decimal[] totales = new decimal[dias];
            int[] cantidades = new int[dias];
            foreach (Venta v in ventas)
            {
                int i = (v.Fecha.Date - desde).Days;
                if (i < 0 || i >= dias) continue;
                totales[i] += v.Total;
                cantidades[i]++;
            }

            List<ValorEtiquetado> lista = new List<ValorEtiquetado>();
            for (int i = 0; i < dias; i++)
            {
                DateTime d = desde.AddDays(i);
                string etiqueta = d == hoy.Date ? "Hoy" : d.ToString("d MMM", Co).Replace(".", string.Empty);
                lista.Add(new ValorEtiquetado { Etiqueta = etiqueta, Valor = totales[i], Cantidad = cantidades[i] });
            }
            return lista;
        }

        /// <summary>Ingresos y gastos de cada uno de los ultimos <paramref name="meses"/> meses (incluye el actual).</summary>
        public static List<ComparacionMensual> IngresosGastosPorMes(List<Movimiento> movimientos, DateTime hoy, int meses)
        {
            meses = Math.Clamp(meses, 1, 24);
            DateTime primero = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));
            List<ComparacionMensual> lista = new List<ComparacionMensual>();
            for (int i = 0; i < meses; i++)
            {
                DateTime m = primero.AddMonths(i);
                string nombre = Co.DateTimeFormat.GetAbbreviatedMonthName(m.Month).Replace(".", string.Empty);
                lista.Add(new ComparacionMensual { Mes = char.ToUpper(nombre[0]) + nombre.Substring(1) });
            }
            foreach (Movimiento mov in movimientos)
            {
                int i = (mov.Fecha.Year - primero.Year) * 12 + mov.Fecha.Month - primero.Month;
                if (i < 0 || i >= meses) continue;
                if (mov.EsIngreso) lista[i].Ingresos += mov.Monto;
                else if (mov.EsGasto) lista[i].Gastos += mov.Monto;
            }
            return lista;
        }

        /// <summary>Cuantos productos tienen stock suficiente, bajo (por debajo del minimo) y agotado.</summary>
        public static List<ValorEtiquetado> EstadoInventario(List<Producto> productos)
        {
            int suficiente = 0, bajo = 0, agotado = 0;
            foreach (Producto p in productos)
            {
                if (p.Stock <= 0) agotado++;
                else if (p.StockBajo) bajo++;   // StockBajo lo calcula Oracle con el minimo del producto
                else suficiente++;
            }
            List<ValorEtiquetado> lista = new List<ValorEtiquetado>();
            if (suficiente > 0) lista.Add(new ValorEtiquetado { Etiqueta = "Stock suficiente", Valor = suficiente, Cantidad = suficiente });
            if (bajo > 0) lista.Add(new ValorEtiquetado { Etiqueta = "Stock bajo", Valor = bajo, Cantidad = bajo });
            if (agotado > 0) lista.Add(new ValorEtiquetado { Etiqueta = "Agotado", Valor = agotado, Cantidad = agotado });
            return lista;
        }

        /// <summary>Deja las <paramref name="maximo"/> categorias mas grandes y suma el resto en "Otras".</summary>
        public static List<ValorEtiquetado> Principales(List<ValorEtiquetado> valores, int maximo)
        {
            List<ValorEtiquetado> ordenados = new List<ValorEtiquetado>(valores);
            ordenados.Sort((a, b) => b.Valor.CompareTo(a.Valor));
            if (ordenados.Count <= maximo) return ordenados;
            List<ValorEtiquetado> lista = ordenados.GetRange(0, maximo - 1);
            ValorEtiquetado otras = new ValorEtiquetado { Etiqueta = "Otras" };
            for (int i = maximo - 1; i < ordenados.Count; i++)
            {
                otras.Valor += ordenados[i].Valor;
                otras.Cantidad += ordenados[i].Cantidad;
            }
            lista.Add(otras);
            return lista;
        }
    }
}
