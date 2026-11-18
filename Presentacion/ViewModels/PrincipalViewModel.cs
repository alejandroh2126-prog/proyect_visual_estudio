using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using MaterialDesignThemes.Wpf;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    /// <summary>Una opcion del menu lateral.</summary>
    public class Seccion
    {
        public string Nombre { get; }
        public PackIconKind Icono { get; }
        public ViewModelBase ViewModel { get; }

        /// <summary>Si es true, la seccion necesita un emprendimiento elegido.</summary>
        public bool RequiereEmprendimiento { get; }

        public Seccion(string nombre, PackIconKind icono, ViewModelBase vm, bool requiereEmprendimiento = true)
        {
            Nombre = nombre;
            Icono = icono;
            ViewModel = vm;
            RequiereEmprendimiento = requiereEmprendimiento;
        }
    }

    /// <summary>Ventana principal: menu, emprendimiento elegido y la seccion que se muestra.</summary>
    public partial class PrincipalViewModel : ViewModelBase
    {
        public string NombreUsuario { get { return Sesion.Usuario?.Nombre ?? string.Empty; } }
        public ObservableCollection<Emprendimiento> Emprendimientos { get; } = new ObservableCollection<Emprendimiento>();
        public List<Seccion> Secciones { get; }

        [ObservableProperty] private Emprendimiento? _emprendimientoActual;
        [ObservableProperty] private Seccion? _seccionActual;
        [ObservableProperty] private bool _sinEmprendimientos;

        public PrincipalViewModel()
        {
            Secciones = new List<Seccion>
            {
                new Seccion("Inicio", PackIconKind.ViewDashboardOutline, new InicioViewModel(), false),
                new Seccion("Emprendimientos", PackIconKind.StorefrontOutline, new EmprendimientosViewModel(RecargarEmprendimientos), false),
                new Seccion("Nómina", PackIconKind.AccountGroupOutline, new NominaViewModel()),
                new Seccion("Vacantes", PackIconKind.BriefcaseAccountOutline, new VacantesViewModel()),
                new Seccion("Inventario", PackIconKind.PackageVariantClosed, new InventarioViewModel()),
                new Seccion("Ventas", PackIconKind.CartOutline, new VentasViewModel()),
                // Finanzas incluye Presupuestos (pestaña): los dos son plata que entra y sale del mismo emprendimiento
                new Seccion("Finanzas", PackIconKind.CashMultiple, new FinanzasViewModel()),
                new Seccion("Reportes", PackIconKind.FilePdfBox, new ReportesViewModel()),
                new Seccion("Asistente", PackIconKind.RobotOutline, new AsistenteViewModel()),
                new Seccion("Telegram", PackIconKind.SendCircleOutline, new TelegramViewModel(), false)
            };
        }

        public override async Task Cargar()
        {
            await RecargarEmprendimientos();
            SeccionActual = Secciones[0];
        }

        /// <summary>Vuelve a leer los emprendimientos (al crear, editar o eliminar uno).</summary>
        public async Task RecargarEmprendimientos()
        {
            _recargando = true;
            int? elegido = EmprendimientoActual?.Id;
            await Ejecutar(async () =>
            {
                List<Emprendimiento> lista = await Fondo.Run(() => App.Servicios.Emprendimientos.Listar(Sesion.IdUsuario));
                Emprendimientos.Clear();
                foreach (Emprendimiento e in lista) Emprendimientos.Add(e);
                SinEmprendimientos = lista.Count == 0;

                Emprendimiento? nuevo = null;
                foreach (Emprendimiento e in lista) if (e.Id == elegido) nuevo = e;
                _recargando = false;
                EmprendimientoActual = nuevo ?? (lista.Count > 0 ? lista[0] : null);
            });
            _recargando = false;
        }

        /// <summary>
        /// Mientras se vuelve a leer la lista, el selector queda vacio un instante (Clear).
        /// Sin esta bandera salia el aviso falso "Primero crea un emprendimiento".
        /// </summary>
        private bool _recargando;

        partial void OnEmprendimientoActualChanged(Emprendimiento? value)
        {
            if (_recargando) return;
            Sesion.Emprendimiento = value;
            // Solo se recarga la seccion si cambio el emprendimiento (no cuando se vuelve a leer la misma lista).
            if (value?.Id == _idCargado) return;
            _idCargado = value?.Id;
            _ = CargarSeccion();
        }

        private int? _idCargado;

        partial void OnSeccionActualChanged(Seccion? value)
        {
            _ = CargarSeccion();
        }

        private async Task CargarSeccion()
        {
            if (SeccionActual == null) return;
            if (SeccionActual.RequiereEmprendimiento && !Sesion.HayEmprendimiento)
            {
                Notificador.Error("Primero crea un emprendimiento en la sección Emprendimientos.");
                return;
            }
            await SeccionActual.ViewModel.Cargar();
        }

        [RelayCommand]
        private void CerrarSesion()
        {
            CerrarSolicitado?.Invoke();
        }

        /// <summary>La ventana se suscribe para cerrarse y volver al inicio de sesion.</summary>
        public System.Action? CerrarSolicitado { get; set; }
    }
}
