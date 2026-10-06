using System;

namespace Logica.Utilidades
{
    /// <summary>Validaciones reutilizables. Una sola mision: validar datos.</summary>
    public static class Validar
    {
        public static void Requerido(string? valor, string campo)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new ReglaDeNegocioException("El campo '" + campo + "' es obligatorio.");
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

        public static void Correo(string? correo)
        {
            Requerido(correo, "Correo");
            string c = correo!.Trim();
            int arroba = c.IndexOf('@');
            int punto = c.LastIndexOf('.');
            if (arroba < 1 || punto < arroba + 2 || punto >= c.Length - 1 || c.Contains(" "))
                throw new ReglaDeNegocioException("El correo no tiene un formato válido.");
        }
    }
}