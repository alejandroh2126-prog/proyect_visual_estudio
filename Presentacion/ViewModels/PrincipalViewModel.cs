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
    public class Seccion : ObservableObject
    {
        public string Nombre { get; }
        public PackIconKind Icono { get; }
        public ViewModelBase ViewModel { get; }

        /// <summary>Si es true, la seccion necesita un emprendimiento elegido.</summary>
        public bool RequiereEmprendimiento { get; }

        /// <summary>Si es true, la seccion es del plan Pro.</summary>
        public bool EsPro { get; }

        private bool _bloqueada;
        /// <summary>Pro y el usuario esta en Gratis: el menu muestra un candado.</summary>
        public bool Bloqueada { get { return _bloqueada; } set { SetProperty(ref _bloqueada, value); } }

        public Seccion(string nombre, PackIconKind icono, ViewModelBase vm, bool requiereEmprendimiento = true, bool esPro = false)
        {
            Nombre = nombre;
            Icono = icono;
            ViewModel = vm;
            RequiereEmprendimiento = requiereEmprendimiento;
            EsPro = esPro;
        }
    }

    /// <summary>Ventana principal: menu, emprendimiento elegido y la seccion que se muestra.</summary>
    public partial class PrincipalViewModel : ViewModelBase
    {
        public string NombreUsuario { get { return Sesion.Usuario?.Nombre ?? string.Empty; } }

        /// <summary>Etiqueta de la barra superior: "Pro · 12 días" o "Plan Gratis".</summary>
        public string TextoPlan
        {
            get
            {
                Usuario? u = Sesion.Usuario;
                if (u == null || !u.EsPro) return "Plan Gratis";
                return u.DiasPro <= 30 ? "Pro · " + u.DiasPro + (u.DiasPro == 1 ? " día" : " días") : "Pro";
            }
        }

        public bool EsPro { get { return Sesion.Usuario?.EsPro == true; } }

        private readonly Seccion _miPlan;
        public ObservableCollection<Emprendimiento> Emprendimientos { get; } = new ObservableCollection<Emprendimiento>();
        public List<Seccion> Secciones { get; }

        [ObservableProperty] private Emprendimiento? _emprendimientoActual;
        [ObservableProperty] private Seccion? _seccionActual;
        [ObservableProperty] private bool _sinEmprendimientos;

        public PrincipalViewModel()
        {
            _miPlan = new Seccion("Mi plan", PackIconKind.CrownOutline, new MiPlanViewModel(ActualizarPlan), false);
            Secciones = new List<Seccion>
            {
                new Seccion("Inicio", PackIconKind.ViewDashboardOutline, new InicioViewModel(), false),
                new Seccion("Emprendimientos", PackIconKind.StorefrontOutline, new EmprendimientosViewModel(RecargarEmprendimientos), false),
                new Seccion("Nómina", PackIconKind.AccountGroupOutline, new NominaViewModel(), true, true),
                new Seccion("Vacantes", PackIconKind.BriefcaseAccountOutline, new VacantesViewModel(), true, true),
                new Seccion("Inventario", PackIconKind.PackageVariantClosed, new InventarioViewModel()),
                new Seccion("Ventas", PackIconKind.CartOutline, new VentasViewModel()),
                // Finanzas incluye Presupuestos (pestaña): los dos son plata que entra y sale del mismo emprendimiento
                new Seccion("Finanzas", PackIconKind.CashMultiple, new FinanzasViewModel()),
                new Seccion("Reportes", PackIconKind.FilePdfBox, new ReportesViewModel(), true, true),
                new Seccion("Asistente", PackIconKind.RobotOutline, new AsistenteViewModel(), true, true),
                new Seccion("Telegram", PackIconKind.SendCircleOutline, new TelegramViewModel(), false, true),
                _miPlan
            };
        }

        public override async Task Cargar()
        {
            // El plan se vuelve a leer de Oracle (pudo vencer o activarse desde otro computador)
            await Ejecutar(async () =>
            {
                int id = Sesion.IdUsuario;
                Sesion.Usuario = await Fondo.Run(() => App.Servicios.Planes.Actual(id));
            });
            ActualizarPlan();
            await RecargarEmprendimientos();
            SeccionActual = Secciones[0];
        }

        /// <summary>Candados del menu y etiqueta del plan (al entrar y al activar Pro).</summary>
        public void ActualizarPlan()
        {
            bool pro = EsPro;
            foreach (Seccion s in Secciones) s.Bloqueada = s.EsPro && !pro;
            OnPropertyChanged(nameof(TextoPlan));
            OnPropertyChanged(nameof(EsPro));
        }

        [RelayCommand]
        private void VerPlan()
        {
            SeccionActual = _miPlan;
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
            if (SeccionActual.EsPro && !EsPro)
            {
                Notificador.Error(SeccionActual.Nombre + " es parte del plan Pro. Actívalo aquí para usarla.");
                SeccionActual = _miPlan;
                return;
            }
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
