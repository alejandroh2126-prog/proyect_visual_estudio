using System;
using System.Globalization;
using System.Text;

namespace Logica.Utilidades
{
    /// <summary>Formatos de dinero, fechas y texto, iguales en cualquier computador.</summary>
    public static class Formato
    {
        private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

        public static string Moneda(decimal valor)
        {
            string n = Math.Round(Math.Abs(valor), 0).ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
            return (valor < 0 ? "-$" : "$") + n;
        }

        public static string Porcentaje(decimal valor)
        {
            return Math.Round(valor, 1).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " %";
        }

        public static string Fecha(DateTime fecha)
        {
            return fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        public static string FechaHora(DateTime fecha)
        {
            return fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>"2026-11" -> "noviembre 2026".</summary>
        public static string NombreMes(string mes)
        {
            if (DateTime.TryParseExact(mes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime f))
                return f.ToString("MMMM yyyy", Colombia);
            return mes;
        }

        /// <summary>Quita tildes y pasa a minusculas (sirve para buscar).</summary>
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            string descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in descompuesto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }

        public static bool Contiene(string? texto, string? busqueda)
        {
            return Normalizar(texto).Contains(Normalizar(busqueda));
        }

        public static string Tamano(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024m).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            return (bytes / 1024m / 1024m).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        public static string EstadoPostulacion(string estado)
        {
            switch (estado)
            {
                case "recibida": return "Recibida";
                case "en_revision": return "En revisión";
                case "aceptada": return "Aceptada";
                case "rechazada": return "Rechazada";
                default: return estado;
            }
        }
    }
}
