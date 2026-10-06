using System;
using System.IO;

namespace Logica.Utilidades
{
    /// <summary>
    /// Carpetas donde la aplicacion guarda todo.
    /// IMPORTANTE: no se usa la carpeta de instalacion (Program Files) porque
    /// Windows no deja escribir ahi sin permisos de administrador.
    /// </summary>
    public static class RutasApp
    {
        public static string CarpetaDatos
        {
            get
            {
                string ruta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SGAPE", "datos");
                Directory.CreateDirectory(ruta);
                return ruta;
            }
        }

        public static string CarpetaFacturas { get { return CarpetaEnDocumentos("Facturas"); } }
        public static string CarpetaReportes { get { return CarpetaEnDocumentos("Reportes"); } }

        private static string CarpetaEnDocumentos(string nombre)
        {
            string ruta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SGAPE", nombre);
            Directory.CreateDirectory(ruta);
            return ruta;
        }
    }
}