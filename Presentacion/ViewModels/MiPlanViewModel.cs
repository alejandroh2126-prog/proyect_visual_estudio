using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    /// <summary>
    /// Modelo de negocio freemium: muestra el plan del usuario y activa Pro con un codigo.
    /// En la demostracion los codigos los crea el administrador en Oracle (sp_sg_generar_codigos);
    /// en un negocio real los entregaria la pasarela de pagos al confirmar el pago.
    /// </summary>
    public partial class MiPlanViewModel : ViewModelBase
    {
        private readonly Action _alCambiarPlan;

        [ObservableProperty] private string? _codigo;

        public MiPlanViewModel(Action alCambiarPlan)
        {
            _alCambiarPlan = alCambiarPlan;
        }

        public bool EsPro { get { return Sesion.Usuario?.EsPro == true; } }
        public bool EsGratis { get { return !EsPro; } }

        public string Titulo { get { return EsPro ? "Tienes el plan Pro" : "Estás en el plan Gratis"; } }

        public string Detalle
        {
            get
            {
                Usuario? u = Sesion.Usuario;
                if (u == null) return string.Empty;
                if (u.EsPro && u.PlanVence != null)
                    return "Vence el " + u.PlanVence.Value.ToString("dd/MM/yyyy") + " (" + u.DiasPro + (u.DiasPro == 1 ? " día" : " días") + "). " +
                           "Si activas otro código, los días se suman.";
                if (u.EsPro) return "Sin fecha de vencimiento.";
                if (u.PlanVence != null)
                    return "Tu Pro venció el " + u.PlanVence.Value.ToString("dd/MM/yyyy") + ". Tus datos siguen guardados: activa un código para volver a usarlos todos.";
                return "Activa Pro con un código para desbloquear todas las funciones.";
            }
        }

        public string PrecioPro { get { return Formato.Moneda(Planes.PrecioProMensual) + " / mes"; } }
        public string[] IncluyeGratis { get { return Planes.IncluyeGratis; } }
        public string[] IncluyePro { get { return Planes.IncluyePro; } }

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                int id = Sesion.IdUsuario;
                Sesion.Usuario = await Fondo.Run(() => App.Servicios.Planes.Actual(id));
            });
            Refrescar();
        }

        [RelayCommand]
        private async Task Activar()
        {
            string codigo = Codigo ?? string.Empty;
            await Ejecutar(async () =>
            {
                int id = Sesion.IdUsuario;
                Sesion.Usuario = await Fondo.Run(() => App.Servicios.Planes.Canjear(id, codigo));
                Codigo = null;
                Refrescar();
                Notificador.Exito("¡Listo! Tienes Pro hasta el " + Sesion.Usuario.PlanVence?.ToString("dd/MM/yyyy") + ".");
            });
        }

        private void Refrescar()
        {
            OnPropertyChanged(nameof(EsPro));
            OnPropertyChanged(nameof(EsGratis));
            OnPropertyChanged(nameof(Titulo));
            OnPropertyChanged(nameof(Detalle));
            _alCambiarPlan();
        }
    }
}
