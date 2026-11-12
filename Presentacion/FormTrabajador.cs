using System.Drawing;
using System.Windows.Forms;
using Logica;
using Presentacion.Comun;
using Presentacion.Vistas;
namespace Presentacion
{
    public class FormTrabajador : Form
    {
        public FormTrabajador(FabricaServicios fabrica)
        {
            Text = "SGAPE - Cuenta de trabajador"; ClientSize = new Size(1150, 720); MinimumSize = new Size(850, 650); StartPosition = FormStartPosition.CenterScreen; BackColor = Tema.Fondo; Font = Tema.Normal;
            VistaEmpleoTrabajador empleo = new VistaEmpleoTrabajador(fabrica); VistaPerfilCuenta cuenta = new VistaPerfilCuenta(fabrica);
            VistaHistorialLaboral historial = new VistaHistorialLaboral(fabrica);
            TabControl tabs = new TabControl { Dock = DockStyle.Fill }; tabs.TabPages.Add(new TabPage("Empleo") { Controls = { empleo } }); tabs.TabPages.Add(new TabPage("Mi cuenta") { Controls = { cuenta } }); tabs.TabPages.Add(new TabPage("Historial laboral") { Controls = { historial } });
            empleo.Mostrar(null);
            tabs.SelectedIndexChanged += (_, _) => { if (tabs.SelectedIndex == 0) empleo.Mostrar(null); else if (tabs.SelectedIndex == 1) cuenta.Mostrar(null); else historial.Mostrar(null); };
            Button salir = Tema.Boton("Cerrar sesión", Tema.VerdeOscuro);
            salir.Click += (_, _) => { if (Mensajes.Confirmar("¿Cerrar la sesión?")) { fabrica.Autenticacion.CerrarSesion(); DialogResult = DialogResult.Retry; } };
            TableLayoutPanel raiz = Tema.ColumnaVertical(); raiz.Fila(Tema.Barra(salir), false); raiz.Fila(tabs, true); Controls.Add(raiz);
        }
    }
}
