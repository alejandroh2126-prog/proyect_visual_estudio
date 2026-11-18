using System.ComponentModel;
using System.Windows;
using Presentacion.ViewModels;

namespace Presentacion.Ventanas
{
    public partial class VentanaPrincipal : Window
    {
        private bool _cerrandoSesion;

        public VentanaPrincipal()
        {
            InitializeComponent();
            PrincipalViewModel vm = new PrincipalViewModel();
            vm.CerrarSolicitado = () =>
            {
                _cerrandoSesion = true;
                Close();
                App.MostrarLogin();
            };
            DataContext = vm;
            Loaded += async (s, e) => await vm.Cargar();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_cerrandoSesion) Application.Current.Shutdown();
        }
    }
}
