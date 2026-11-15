using System;

namespace Logica.Utilidades
{
	/// <summary>
	/// Error "esperado" del negocio (dato invalido, stock insuficiente, etc.).
	/// La capa de Presentacion muestra su mensaje tal cual al usuario.
	/// </summary>
	public class ReglaDeNegocioException : Exception
	{
		public ReglaDeNegocioException(string mensaje) : base(mensaje)
		{
		}
	}
}