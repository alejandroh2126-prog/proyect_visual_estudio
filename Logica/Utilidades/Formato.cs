using System;
using System.Globalization;
using System.Text;

namespace Logica.Utilidades
{
    /// <summary>Formatos de dinero y texto, iguales en cualquier computador.</summary>
    public static class Formato
    {
        public static string Moneda(decimal valor)
        {
            string n = Math.Round(valor, 0).ToString("N0", CultureInfo.InvariantCulture);
            return "$" + n.Replace(',', '.');
        }

        public static string Fecha(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string FechaHora(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Quita tildes y pasa a minusculas (sirve para buscar y para el asistente).</summary>
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            string descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool Contiene(string? texto, string? busqueda)
        {
            return Normalizar(texto).Contains(Normalizar(busqueda));
        }
    }
}