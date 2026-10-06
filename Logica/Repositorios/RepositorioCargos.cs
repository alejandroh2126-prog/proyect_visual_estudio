using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    public class RepositorioCargos : RepositorioCsv<Cargo>
    {
        public RepositorioCargos() : base("cargos.csv") { }

        protected override string Serializar(Cargo c)
        {
            return CsvUtil.Unir(CsvUtil.DeEntero(c.Id), CsvUtil.DeEntero(c.EmprendimientoId), c.Nombre,
                CsvUtil.DeDecimal(c.SalarioBase), c.Descripcion);
        }

        protected override Cargo? Deserializar(string[] c)
        {
            return new Cargo
            {
                Id = CsvUtil.AEntero(c[0]),
                EmprendimientoId = CsvUtil.AEntero(c[1]),
                Nombre = c[2],
                SalarioBase = CsvUtil.ADecimal(c[3]),
                Descripcion = c[4]
            };
        }

        protected override int ObtenerId(Cargo c) { return c.Id; }
        protected override void AsignarId(Cargo c, int id) { c.Id = id; }
    }
}