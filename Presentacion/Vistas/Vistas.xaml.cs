using System.Windows;
using System.Windows.Controls;

namespace Presentacion.Vistas
{
    // Las vistas no tienen logica: todo esta en su ViewModel (MVVM).

    /// <summary>Barra comun de las listas. "Extra" permite agregar botones propios del modulo.</summary>
    public partial class BarraCrud : UserControl
    {
        public static readonly DependencyProperty ExtraProperty =
            DependencyProperty.Register(nameof(Extra), typeof(object), typeof(BarraCrud), new PropertyMetadata(null));

        public object? Extra
        {
            get { return GetValue(ExtraProperty); }
            set { SetValue(ExtraProperty, value); }
        }

        public BarraCrud() { InitializeComponent(); }
    }

    public partial class InicioView : UserControl { public InicioView() { InitializeComponent(); } }
    public partial class EmprendimientosView : UserControl { public EmprendimientosView() { InitializeComponent(); } }
    public partial class NominaView : UserControl { public NominaView() { InitializeComponent(); } }
    public partial class VacantesView : UserControl { public VacantesView() { InitializeComponent(); } }
    public partial class InventarioView : UserControl { public InventarioView() { InitializeComponent(); } }
    public partial class VentasView : UserControl { public VentasView() { InitializeComponent(); } }
    public partial class FinanzasView : UserControl { public FinanzasView() { InitializeComponent(); } }
    public partial class PresupuestosView : UserControl { public PresupuestosView() { InitializeComponent(); } }
    public partial class ReportesView : UserControl { public ReportesView() { InitializeComponent(); } }
    public partial class TelegramView : UserControl { public TelegramView() { InitializeComponent(); } }
}
