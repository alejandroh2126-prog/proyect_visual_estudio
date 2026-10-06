using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    public class RepositorioProductos : RepositorioCsv<Producto>
    {
        public RepositorioProductos() : base("productos.csv") { }

        protected override string Serializar(Producto p)
        {
            return CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.EmprendimientoId), p.Nombre,
                p.Categoria, CsvUtil.DeDecimal(p.PrecioCompra), CsvUtil.DeDecimal(p.PrecioVenta),
                CsvUtil.DeEntero(p.Stock), CsvUtil.DeEntero(p.StockMinimo));
        }

        protected override Producto? Deserializar(string[] c)
        {
            Producto p = new Producto
            {
                Id = CsvUtil.AEntero(c[0]),
                EmprendimientoId = CsvUtil.AEntero(c[1]),
                Nombre = c[2],
                Categoria = c[3],
                PrecioCompra = CsvUtil.ADecimal(c[4]),
                PrecioVenta = CsvUtil.ADecimal(c[5]),
                StockMinimo = CsvUtil.AEntero(c[7])
            };
            p.EstablecerStock(CsvUtil.AEntero(c[6]));
            return p;
        }

        protected override int ObtenerId(Producto p) { return p.Id; }
        protected override void AsignarId(Producto p, int id) { p.Id = id; }
    }
}