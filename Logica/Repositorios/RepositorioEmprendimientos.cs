using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
	public class RepositorioEmprendimientos : RepositorioCsv<Emprendimiento>
	{
		public RepositorioEmprendimientos() : base("emprendimientos.csv") { }

		protected override string Serializar(Emprendimiento e)
		{
			return CsvUtil.Unir(CsvUtil.DeEntero(e.Id), CsvUtil.DeEntero(e.UsuarioId), e.Nombre,
				e.Descripcion, e.Sector, CsvUtil.DeFecha(e.FechaInicio), e.Estado);
		}

		protected override Emprendimiento? Deserializar(string[] c)
		{
			return new Emprendimiento
			{
				Id = CsvUtil.AEntero(c[0]),
				UsuarioId = CsvUtil.AEntero(c[1]),
				Nombre = c[2],
				Descripcion = c[3],
				Sector = c[4],
				FechaInicio = CsvUtil.AFecha(c[5]),
				Estado = c[6]
			};
		}

		protected override int ObtenerId(Emprendimiento e) { return e.Id; }
		protected override void AsignarId(Emprendimiento e, int id) { e.Id = id; }
	}
}