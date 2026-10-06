using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    public class RepositorioPresupuestos : RepositorioCsv<Presupuesto>
    {
        public RepositorioPresupuestos() : base("presupuestos.csv") { }

        protected override string Serializar(Presupuesto p)
        {
            return CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.EmprendimientoId), p.Categoria,
                CsvUtil.DeDecimal(p.MontoLimite), p.Mes);
        }

        protected override Presupuesto? Deserializar(string[] c)
        {
            return new Presupuesto
            {
                Id = CsvUtil.AEntero(c[0]),
                EmprendimientoId = CsvUtil.AEntero(c[1]),
                Categoria = c[2],
                MontoLimite = CsvUtil.ADecimal(c[3]),
                Mes = c[4]
            };
        }

        protected override int ObtenerId(Presupuesto p) { return p.Id; }
        protected override void AsignarId(Presupuesto p, int id) { p.Id = id; }
    }
}