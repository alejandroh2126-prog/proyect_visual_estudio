using System;
using System.IO;
using System.Text;
using Entidades;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>UNA SOLA MISION: crear el archivo de texto de la factura de una venta.</summary>
    public class ServicioFacturas
    {
        public string Generar(Venta venta, Emprendimiento emprendimiento)
        {
            string ruta = Path.Combine(RutasApp.CarpetaFacturas, "factura_" + venta.Id + ".txt");
            StringBuilder sb = new StringBuilder();
            string linea = new string('-', 60);

            sb.AppendLine("SGAPE - FACTURA DE VENTA");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine("Emprendimiento : " + emprendimiento.Nombre);
            sb.AppendLine("Sector         : " + emprendimiento.Sector);
            sb.AppendLine("Fecha          : " + Formato.FechaHora(venta.Fecha));
            sb.AppendLine("Factura No.    : " + venta.Id);
            sb.AppendLine("Observación    : " + venta.Observacion);
            sb.AppendLine(linea);
            sb.AppendLine(string.Format("{0,-28}{1,6}{2,12}{3,14}", "PRODUCTO", "CANT", "PRECIO", "SUBTOTAL"));
            sb.AppendLine(linea);
            foreach (DetalleVenta d in venta.Detalles)
            {
                string nombre = d.ProductoNombre.Length > 27 ? d.ProductoNombre.Substring(0, 27) : d.ProductoNombre;
                sb.AppendLine(string.Format("{0,-28}{1,6}{2,12}{3,14}", nombre, d.Cantidad,
                    Formato.Moneda(d.PrecioUnitario), Formato.Moneda(d.Subtotal)));
            }
            sb.AppendLine(linea);
            sb.AppendLine(string.Format("{0,-28}{1,6}{2,12}{3,14}", "TOTAL", venta.TotalProductos, "",
                Formato.Moneda(venta.Total)));
            sb.AppendLine(linea);
            sb.AppendLine();
            sb.AppendLine("¡Gracias por su compra!");
            sb.AppendLine("Universidad Popular del Cesar - SGAPE - " + DateTime.Now.Year);

            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(false));
            return ruta;
        }
    }
}