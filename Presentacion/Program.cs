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
            using System.Threading.Mutex mutex = new System.Threading.Mutex(true, @"Local\SGAPE-Datos-UnaInstancia", out bool primera);
            if (!primera) { MessageBox.Show("SGAPE ya está abierto. Cierra sesión para cambiar de cuenta.", "SGAPE"); return; }
            FabricaServicios fabrica = new FabricaServicios();
            fabrica.AsistenteIA.IniciarOllama();   // abre la IA local en segundo plano

            try
            {
                while (true)
                {
                    fabrica.AsistenteNube.ReiniciarConversacion();
                    Presentacion.Comun.Tema.Aplicar(null);
                    using (FormLogin login = new FormLogin(fabrica))
                    {
                        if (login.ShowDialog() != DialogResult.OK) return;
                    }

                    Presentacion.Comun.Mensajes.Ejecutar(() => Presentacion.Comun.Tema.Aplicar(fabrica.PerfilCuenta.Obtener()));
                    using (Form principal = Logica.Utilidades.Sesion.UsuarioActual?.Rol == Entidades.RolUsuario.Trabajador
                        ? new FormTrabajador(fabrica) : new FormPrincipal(fabrica))
                    {
                        principal.ShowDialog();
                        if (principal.DialogResult != DialogResult.Retry) return;
                    }
                }
            }
            finally
            {
                try { fabrica.AsistenteIA.DetenerOllama(); } finally { mutex.ReleaseMutex(); }
            }
        }
    }
}