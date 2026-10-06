namespace Entidades
{
    /// <summary>
    /// Categorias de ingresos y gastos. Se usan al registrar movimientos y para los presupuestos.
    /// Una sola responsabilidad: ser el catalogo oficial de categorias.
    /// </summary>
    public static class Categorias
    {
        public const string Ventas = "Ventas";
        public const string Inventario = "Inventario";

        public static readonly string[] Ingresos =
        {
            "Ventas", "Servicios", "Aportes de socios", "Otros ingresos"
        };

        public static readonly string[] Gastos =
        {
            "Inventario", "Nómina", "Arriendo", "Servicios públicos",
            "Marketing", "Transporte", "Impuestos", "Otros gastos"
        };
    }
}