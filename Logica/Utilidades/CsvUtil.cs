using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Logica.Utilidades
{
    /// <summary>
    /// Lectura/escritura segura de lineas CSV (separador ';').
    /// Corrige el problema del proyecto en Java: si un texto tenia coma, se danaba el archivo.
    /// </summary>
    public static class CsvUtil
    {
        public const char Separador = ';';
        private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

        public static string Escapar(string? valor)
        {
            string v = (valor ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            if (v.IndexOf(Separador) >= 0 || v.IndexOf('"') >= 0)
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        public static string Unir(params string[] campos)
        {
            string[] escapados = new string[campos.Length];
            for (int i = 0; i < campos.Length; i++) escapados[i] = Escapar(campos[i]);
            return string.Join(Separador.ToString(), escapados);
        }

        public static string[] Dividir(string linea)
        {
            List<string> campos = new List<string>();
            StringBuilder actual = new StringBuilder();
            bool enComillas = false;

            for (int i = 0; i < linea.Length; i++)
            {
                char c = linea[i];
                if (enComillas)
                {
                    if (c == '"')
                    {
                        if (i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                        else enComillas = false;
                    }
                    else actual.Append(c);
                }
                else
                {
                    if (c == '"') enComillas = true;
                    else if (c == Separador) { campos.Add(actual.ToString()); actual.Clear(); }
                    else actual.Append(c);
                }
            }
            campos.Add(actual.ToString());
            return campos.ToArray();
        }

        public static string DeDecimal(decimal valor) { return valor.ToString(Cultura); }
        public static string DeEntero(int valor) { return valor.ToString(Cultura); }
        public static string DeFecha(DateTime fecha) { return fecha.ToString("yyyy-MM-dd HH:mm:ss", Cultura); }

        public static decimal ADecimal(string texto) { return decimal.Parse(texto.Trim(), Cultura); }
        public static int AEntero(string texto) { return int.Parse(texto.Trim(), Cultura); }

        public static DateTime AFecha(string texto)
        {
            return DateTime.Parse(texto.Trim(), Cultura, DateTimeStyles.None);
        }
    }
}