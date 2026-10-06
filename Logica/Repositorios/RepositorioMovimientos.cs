using System;
using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
	public class RepositorioMovimientos : RepositorioCsv<Movimiento>
	{
		public RepositorioMovimientos() : base("movimientos.csv") { }

		protected override string Serializar(Movimiento m)
		{
			return CsvUtil.Unir(CsvUtil.DeEntero(m.Id), CsvUtil.DeEntero(m.EmprendimientoId),
				CsvUtil.DeFecha(m.Fecha), m.Tipo.ToString(), m.Descripcion,
				CsvUtil.DeDecimal(m.Monto), m.Referencia, m.Categoria);
		}

		protected override Movimiento? Deserializar(string[] c)
		{
			return new Movimiento
			{
				Id = CsvUtil.AEntero(c[0]),
				EmprendimientoId = CsvUtil.AEntero(c[1]),
				Fecha = CsvUtil.AFecha(c[2]),
				Tipo = (TipoMovimiento)Enum.Parse(typeof(TipoMovimiento), c[3]),
				Descripcion = c[4],
				Monto = CsvUtil.ADecimal(c[5]),
				Referencia = c[6],
				Categoria = c.Length > 7 ? c[7] : string.Empty
			};
		}

		protected override int ObtenerId(Movimiento m) { return m.Id; }
		protected override void AsignarId(Movimiento m, int id) { m.Id = id; }
	}
}