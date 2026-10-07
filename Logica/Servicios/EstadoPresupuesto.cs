using Entidades;

namespace Logica.Servicios
{
	/// <summary>Un presupuesto junto con lo que ya se gasto en ese mes (solo datos).</summary>
	public class EstadoPresupuesto
	{
		public Presupuesto Presupuesto { get; set; } = new Presupuesto();
		public decimal Gastado { get; set; }

		public decimal Disponible { get { return Presupuesto.MontoLimite - Gastado; } }

		public decimal Porcentaje
		{
			get
			{
				if (Presupuesto.MontoLimite <= 0m) return 0m;
				return Gastado / Presupuesto.MontoLimite * 100m;
			}
		}

		public bool Excedido { get { return Gastado > Presupuesto.MontoLimite; } }
		public bool EnAlerta { get { return !Excedido && Porcentaje >= 80m; } }
	}
}