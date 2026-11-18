using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Entidades;

namespace Logica.Utilidades
{
    /// <summary>
    /// Validaciones de la capa Logica (reglas de negocio). Son la segunda de tres barreras:
    /// 1) la interfaz (campos obligatorios, solo numeros), 2) aqui, 3) Oracle (CHECK, UNIQUE y disparadores).
    /// Todas lanzan ReglaDeNegocioException con un mensaje listo para mostrar.
    /// </summary>
    public static class Validar
    {
        private static readonly Regex RegexCorreo = new Regex(@"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex RegexTelefono = new Regex(@"^[0-9]{7,10}$", RegexOptions.Compiled);
        private static readonly Regex RegexMes = new Regex(@"^[0-9]{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);
        private static readonly Regex RegexNombrePersona = new Regex(@"^[\p{L}][\p{L} '\-\.]*$", RegexOptions.Compiled);

        public static string Requerido(string? valor, string campo, int maximo = 0)
        {
            string texto = (valor ?? string.Empty).Trim();
            if (texto.Length == 0)
                throw new ReglaDeNegocioException("El campo '" + campo + "' es obligatorio.");
            if (maximo > 0 && texto.Length > maximo)
                throw new ReglaDeNegocioException("El campo '" + campo + "' admite máximo " + maximo + " caracteres.");
            NoTieneCaracteresDeControl(texto, campo);
            return texto;
        }

        public static string Opcional(string? valor, string campo, int maximo)
        {
            string texto = (valor ?? string.Empty).Trim();
            if (texto.Length > maximo)
                throw new ReglaDeNegocioException("El campo '" + campo + "' admite máximo " + maximo + " caracteres.");
            NoTieneCaracteresDeControl(texto, campo);
            return texto;
        }

        public static string NombrePersona(string? valor, string campo)
        {
            string texto = Requerido(valor, campo, 100);
            if (!RegexNombrePersona.IsMatch(texto))
                throw new ReglaDeNegocioException("El campo '" + campo + "' solo admite letras y espacios.");
            return texto;
        }

        public static void Seleccionado(object? valor, string campo)
        {
            if (valor == null || (valor is int n && n <= 0) || (valor is string s && s.Length == 0))
                throw new ReglaDeNegocioException("Elige un valor de la lista en '" + campo + "'.");
        }

        public static void MayorQueCero(decimal valor, string campo)
        {
            if (valor <= 0m)
                throw new ReglaDeNegocioException("El campo '" + campo + "' debe ser mayor que cero.");
        }

        public static void NoNegativo(decimal valor, string campo)
        {
            if (valor < 0m)
                throw new ReglaDeNegocioException("El campo '" + campo + "' no puede ser negativo.");
        }

        public static void Maximo(decimal valor, decimal maximo, string campo)
        {
            if (valor > maximo)
                throw new ReglaDeNegocioException("El campo '" + campo + "' no puede superar " + Formato.Moneda(maximo) + ".");
        }

        public static void FechaNoFutura(DateTime fecha, string campo)
        {
            if (fecha.Date > DateTime.Today)
                throw new ReglaDeNegocioException("La fecha '" + campo + "' no puede ser futura.");
            if (fecha.Year < 1950)
                throw new ReglaDeNegocioException("La fecha '" + campo + "' no es válida.");
        }

        public static string Correo(string? correo, bool obligatorio = true)
        {
            string c = (correo ?? string.Empty).Trim().ToLowerInvariant();
            if (c.Length == 0)
            {
                if (obligatorio) throw new ReglaDeNegocioException("El campo 'Correo' es obligatorio.");
                return c;
            }
            if (c.Length > 150 || !RegexCorreo.IsMatch(c))
                throw new ReglaDeNegocioException("El correo no tiene un formato válido (ejemplo: nombre@correo.com).");
            return c;
        }

        public static string Telefono(string? telefono, bool obligatorio)
        {
            string t = (telefono ?? string.Empty).Replace(" ", "").Replace("-", "").Trim();
            if (t.Length == 0)
            {
                if (obligatorio) throw new ReglaDeNegocioException("El campo 'Teléfono' es obligatorio.");
                return t;
            }
            if (!RegexTelefono.IsMatch(t))
                throw new ReglaDeNegocioException("El teléfono debe tener entre 7 y 10 números, sin letras.");
            return t;
        }

        /// <summary>Valida el numero con el patron del tipo de documento (el mismo que usa Oracle).</summary>
        public static string Documento(TipoDocumento? tipo, string? numero)
        {
            if (tipo == null) throw new ReglaDeNegocioException("Elige el tipo de documento.");
            string n = (numero ?? string.Empty).Replace(".", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (n.Length == 0) throw new ReglaDeNegocioException("Escribe el número de documento.");
            if (!Regex.IsMatch(n, tipo.Patron))
                throw new ReglaDeNegocioException("El número no es válido para " + tipo.Nombre + ". " + tipo.Ayuda);
            return n;
        }

        public static void Clave(string? clave)
        {
            string c = clave ?? string.Empty;
            if (c.Length < 8)
                throw new ReglaDeNegocioException("La contraseña debe tener al menos 8 caracteres.");
            bool letra = false, numero = false;
            foreach (char ch in c)
            {
                if (char.IsLetter(ch)) letra = true;
                if (char.IsDigit(ch)) numero = true;
            }
            if (!letra || !numero)
                throw new ReglaDeNegocioException("La contraseña debe tener letras y números.");
        }

        public static string Mes(string? mes)
        {
            string m = (mes ?? string.Empty).Trim();
            if (!RegexMes.IsMatch(m))
                throw new ReglaDeNegocioException("El mes debe tener el formato aaaa-mm (ejemplo: 2026-11).");
            return m;
        }

        public static void EnteroPositivo(int valor, string campo, int maximo = 1_000_000)
        {
            if (valor <= 0) throw new ReglaDeNegocioException("El campo '" + campo + "' debe ser un número entero mayor que cero.");
            if (valor > maximo) throw new ReglaDeNegocioException("El campo '" + campo + "' es demasiado grande.");
        }

        private static void NoTieneCaracteresDeControl(string texto, string campo)
        {
            foreach (char ch in texto)
            {
                if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
                    throw new ReglaDeNegocioException("El campo '" + campo + "' tiene caracteres no permitidos.");
            }
        }

        public static string MesActual()
        {
            return DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
    }
}
