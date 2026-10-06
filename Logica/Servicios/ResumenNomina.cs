namespace Logica.Servicios
{
	/// <summary>Resultado de calcular la nomina de un emprendimiento (solo datos).</summary>
	public class ResumenNomina
	{
		public int TrabajadoresActivos { get; set; }
		public decimal NominaMensualBruta { get; set; }
		public decimal NominaQuincenal { get; set; }
		public decimal TotalDeducciones { get; set; }
		public decimal NominaNeta { get; set; }
	}
}