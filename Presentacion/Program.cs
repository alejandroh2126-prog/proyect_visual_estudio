using System;
using System.Windows.Forms;
using Logica;

namespace Presentacion
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada. Crea la fabrica de servicios UNA vez y se la pasa a las ventanas.
        /// Si el usuario pulsa "Cerrar sesion" vuelve al login; si cierra la ventana, el programa termina.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            FabricaServicios fabrica = new FabricaServicios();

            while (true)
            {
                using (FormLogin login = new FormLogin(fabrica))
                {
                    if (login.ShowDialog() != DialogResult.OK) return;
                }

                using (FormPrincipal principal = new FormPrincipal(fabrica))
                {
                    principal.ShowDialog();
                    if (principal.DialogResult != DialogResult.Retry) return;
                }
            }
        }
    }
}