using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    /// <summary>
    /// PILAR: HERENCIA + POLIMORFISMO. Toda pantalla de lista (emprendimientos, cargos, trabajadores,
    /// productos, vacantes, presupuestos...) hereda de aqui: Nuevo, Editar, Eliminar, Buscar y Recargar
    /// se escriben UNA vez. Cada modulo solo dice como consultar, que campos tiene su formulario y como guardar.
    /// </summary>
    public abstract partial class CrudViewModel<T> : ViewModelBase where T : class
    {
        public ObservableCollection<T> Elementos { get; } = new ObservableCollection<T>();
        public ICollectionView Vista { get; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
        [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
        private T? _seleccionado;

        [ObservableProperty]
        private string _filtro = string.Empty;

        /// <summary>Nombre del elemento para los titulos ("producto", "cargo"...).</summary>
        protected abstract string Nombre { get; }

        protected CrudViewModel()
        {
            Vista = CollectionViewSource.GetDefaultView(Elementos);
            Vista.Filter = o => string.IsNullOrWhiteSpace(Filtro) || Coincide((T)o, Filtro);
        }

        partial void OnFiltroChanged(string value) { Vista.Refresh(); }

        protected abstract Task<List<T>> Consultar();
        protected abstract bool Coincide(T elemento, string filtro);

        /// <summary>Formulario para crear (elemento = null) o editar.</summary>
        protected abstract Task<FormularioViewModel?> CrearFormulario(T? elemento);
        protected abstract Task EliminarElemento(T elemento);
        protected virtual string DescribirParaEliminar(T elemento) { return "este " + Nombre; }

        /// <summary>Si es false, la pantalla no permite crear (por ejemplo, sin emprendimiento elegido).</summary>
        protected virtual bool PuedeCrear() { return true; }

        public override Task Cargar() { return Recargar(); }

        [RelayCommand]
        protected async Task Recargar()
        {
            await Ejecutar(async () =>
            {
                List<T> lista = await Consultar();
                Elementos.Clear();
                foreach (T e in lista) Elementos.Add(e);
                await DespuesDeCargar();
            });
        }

        protected virtual Task DespuesDeCargar() { return Task.CompletedTask; }

        [RelayCommand]
        private async Task Nuevo()
        {
            if (!PuedeCrear()) return;
            FormularioViewModel? f = null;
            await Ejecutar(async () => { f = await CrearFormulario(null); });
            if (f != null && await Notificador.Formulario(f))
            {
                Notificador.Exito("Se creó el " + Nombre + ".");
                await Recargar();
            }
        }

        private bool HaySeleccion() { return Seleccionado != null; }

        [RelayCommand(CanExecute = nameof(HaySeleccion))]
        private async Task Editar()
        {
            if (Seleccionado == null) return;
            FormularioViewModel? f = null;
            T elemento = Seleccionado;
            await Ejecutar(async () => { f = await CrearFormulario(elemento); });
            if (f != null && await Notificador.Formulario(f))
            {
                Notificador.Exito("Se guardaron los cambios.");
                await Recargar();
            }
        }

        [RelayCommand(CanExecute = nameof(HaySeleccion))]
        private async Task Eliminar()
        {
            if (Seleccionado == null) return;
            T elemento = Seleccionado;
            if (!await Notificador.Confirmar("Eliminar " + Nombre, "¿Seguro que quieres eliminar " + DescribirParaEliminar(elemento) +
                                             "? Esta acción no se puede deshacer.", "Sí, eliminar"))
                return;
            await Ejecutar(async () =>
            {
                await EliminarElemento(elemento);
                Notificador.Exito("Se eliminó el " + Nombre + ".");
            });
            await Recargar();
        }
    }
}
