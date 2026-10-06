namespace Entidades
{
    /// <summary>
    /// Valores permitidos para el estado de un emprendimiento o trabajador.
    /// Una sola responsabilidad: centralizar los textos de estado (SRP).
    /// </summary>
    public static class Estados
    {
        public const string Activo = "activo";
        public const string Inactivo = "inactivo";
    }
}