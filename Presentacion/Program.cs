using System;
using System.Windows.Forms;

namespace Presentacion
{
	internal static class Program
	{
		// Version TEMPORAL: solo para que la solucion compile mientras construimos las pantallas.
		// En el Commit 21 la reemplazamos por la version final.
		[STAThread]
		private static void Main()
		{
			ApplicationConfiguration.Initialize();
			MessageBox.Show("SGAPE: la interfaz se esta construyendo.");
		}
	}
}