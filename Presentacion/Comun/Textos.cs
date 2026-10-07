using Entidades;

namespace Presentacion.Comun
{
	/// <summary>Convierte valores internos en texto amigable para mostrar en pantalla.</summary>
	public static class Textos
	{
		public static string Tipo(TipoMovimiento tipo)
		{
			switch (tipo)
			{
				case TipoMovimiento.Venta: return "Venta";
				case TipoMovimiento.Compra: return "Compra";
				case TipoMovimiento.AjusteStock: return "Ajuste de stock";
				case TipoMovimiento.Gasto: return "Gasto";
				default: return "Ingreso";
			}
		}
	}
}