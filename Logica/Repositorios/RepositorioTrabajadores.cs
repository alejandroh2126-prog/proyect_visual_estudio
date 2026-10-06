using System.Collections.Generic;
using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
	/// <summary>
	/// El trabajador guarda solo el Id de su cargo; al leer, se vuelve a armar el objeto Cargo.
	/// </summary>
	public class RepositorioTrabajadores : RepositorioCsv<Trabajador>
	{
		private readonly RepositorioCargos _repositorioCargos;
		private Dictionary<int, Cargo> _cargos = new Dictionary<int, Cargo>();

		public RepositorioTrabajadores(RepositorioCargos repositorioCargos) : base("trabajadores.csv")
		{
			_repositorioCargos = repositorioCargos;
		}

		protected override void AntesDeCargar()
		{
			_cargos = new Dictionary<int, Cargo>();
			foreach (Cargo cargo in _repositorioCargos.Listar()) _cargos[cargo.Id] = cargo;
		}

		protected override string Serializar(Trabajador t)
		{
			return CsvUtil.Unir(CsvUtil.DeEntero(t.Id), CsvUtil.DeEntero(t.EmprendimientoId), t.Nombre,
				t.Apellido, t.Cedula, CsvUtil.DeEntero(t.Cargo.Id), CsvUtil.DeFecha(t.FechaIngreso), t.Estado);
		}

		protected override Trabajador? Deserializar(string[] c)
		{
			int cargoId = CsvUtil.AEntero(c[5]);
			if (!_cargos.ContainsKey(cargoId)) return null;

			Trabajador t = new Trabajador(CsvUtil.AEntero(c[0]), c[2], c[3], c[4], _cargos[cargoId],
				CsvUtil.AFecha(c[6]), CsvUtil.AEntero(c[1]));
			t.Estado = c[7];
			return t;
		}

		protected override int ObtenerId(Trabajador t) { return t.Id; }
		protected override void AsignarId(Trabajador t, int id) { t.Id = id; }
	}
}