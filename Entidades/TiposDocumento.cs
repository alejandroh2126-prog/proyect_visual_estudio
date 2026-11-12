using System;
namespace Entidades
{
    public static class TiposDocumento
    {
        public static readonly string[] Valores = { "CC", "CE", "Pasaporte", "PPT", "PEP", "Salvoconducto", "Otro" };
        public static bool EsValido(string tipo) => Array.IndexOf(Valores, tipo) >= 0;
    }
}
