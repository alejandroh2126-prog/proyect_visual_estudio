using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Presentacion.ViewModels;

namespace Presentacion.Vistas
{
    /// <summary>Solo detalles de pantalla: bajar el chat al ultimo mensaje y pasar la clave del PasswordBox al ViewModel.</summary>
    public partial class AsistenteView : UserControl
    {
        private AsistenteViewModel? _vm;

        public AsistenteView()
        {
            InitializeComponent();
            DataContextChanged += AlCambiarContexto;
            CajaClave.PasswordChanged += (s, e) => { if (_vm != null) _vm.ClaveNueva = CajaClave.Password; };
        }

        private void AlCambiarContexto(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.Mensajes.CollectionChanged -= AlCambiarMensajes;
            _vm = e.NewValue as AsistenteViewModel;
            if (_vm != null) _vm.Mensajes.CollectionChanged += AlCambiarMensajes;
        }

        private void AlCambiarMensajes(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Action(() => Desplazador.ScrollToEnd()), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
