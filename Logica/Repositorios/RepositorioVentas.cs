using System;
using System.Collections.Generic;
using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    /// <summary>
    /// Cada venta ocupa una linea. Los detalles van en el ultimo campo:
    /// idProducto|nombre|cantidad|precio separados por '~'. El nombre se codifica
    /// para que no pueda romper el formato.
    /// </summary>
    public class RepositorioVentas : RepositorioCsv<Venta>
    {
        public RepositorioVentas() : base("ventas.csv") { }

        protected override string Serializar(Venta v)
        {
            List<string> detalles = new List<string>();
            foreach (DetalleVenta d in v.Detalles)
            {
                detalles.Add(CsvUtil.DeEntero(d.ProductoId) + "|" + Uri.EscapeDataString(d.ProductoNombre) + "|" +
                             CsvUtil.DeEntero(d.Cantidad) + "|" + CsvUtil.DeDecimal(d.PrecioUnitario));
            }
            return CsvUtil.Unir(CsvUtil.DeEntero(v.Id), CsvUtil.DeEntero(v.EmprendimientoId),
                CsvUtil.DeFecha(v.Fecha), v.Observacion, string.Join("~", detalles));
        }

        protected override Venta? Deserializar(string[] c)
        {
            Venta v = new Venta
            {
                Id = CsvUtil.AEntero(c[0]),
                EmprendimientoId = CsvUtil.AEntero(c[1]),
                Fecha = CsvUtil.AFecha(c[2]),
                Observacion = c[3]
            };

            if (!string.IsNullOrWhiteSpace(c[4]))
            {
                foreach (string texto in c[4].Split('~'))
                {
                    if (string.IsNullOrWhiteSpace(texto)) continue;
                    string[] p = texto.Split('|');
                    v.Detalles.Add(new DetalleVenta
                    {
                        ProductoId = CsvUtil.AEntero(p[0]),
                        ProductoNombre = Uri.UnescapeDataString(p[1]),
                        Cantidad = CsvUtil.AEntero(p[2]),
                        PrecioUnitario = CsvUtil.ADecimal(p[3])
                    });
                }
            }
            return v;
        }

        protected override int ObtenerId(Venta v) { return v.Id; }
        protected override void AsignarId(Venta v, int id) { v.Id = id; }
    }
}