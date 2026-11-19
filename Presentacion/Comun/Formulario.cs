using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using MaterialDesignThemes.Wpf;

namespace Presentacion.Comun
{
    public enum TipoCampo
    {
        Texto,
        TextoLargo,
        Entero,
        Moneda,
        Decimal,
        Fecha,

        /// <summary>Lista cerrada (ComboBox no editable).</summary>
        Lista,

        /// <summary>Lista con texto predictivo: se escribe y se sugiere, pero hay que elegir un elemento.</summary>
        ListaPredictiva,

        /// <summary>Texto libre con sugerencias (por ejemplo la categoria de un producto).</summary>
        TextoPredictivo
    }

    /// <summary>
    /// Un campo de la ventana generica de crear/editar. Cada modulo describe sus campos y
    /// la MISMA ventana (FormularioDialogo) los dibuja: no hay un formulario distinto por modulo.
    /// </summary>
    public partial class CampoFormulario : ObservableObject
    {
        public string Clave { get; }
        public string Etiqueta { get; }
        public TipoCampo Tipo { get; }
        public bool Requerido { get; init; } = true;
        public int MaxLongitud { get; init; } = 150;
        public bool PermiteNegativos { get; init; }

        [ObservableProperty] private string _texto = string.Empty;
        [ObservableProperty] private DateTime? _fecha;
        [ObservableProperty] private object? _seleccion;
        [ObservableProperty] private IEnumerable? _opciones;
        [ObservableProperty] private string? _ayuda;
        [ObservableProperty] private string? _error;
        [ObservableProperty] private bool _habilitado = true;

        /// <summary>Campos que solo aparecen segun otra eleccion (por ejemplo el % a devolver de un aporte). Oculto no se valida.</summary>
        [ObservableProperty] private bool _visible = true;

        /// <summary>Se ejecuta cuando cambia la seleccion (por ejemplo: elegir un cargo sugerido llena el salario).</summary>
        public Action<object?>? AlSeleccionar { get; init; }

        /// <summary>Se ejecuta cuando cambia el texto (por ejemplo: recalcular cuanto se devuelve).</summary>
        public Action<string>? AlEscribir { get; init; }

        public CampoFormulario(string clave, string etiqueta, TipoCampo tipo)
        {
            Clave = clave;
            Etiqueta = etiqueta;
            Tipo = tipo;
        }

        public string Pista { get { return Requerido ? Etiqueta + " *" : Etiqueta; } }

        public bool EsNumerico { get { return Tipo == TipoCampo.Entero || Tipo == TipoCampo.Moneda || Tipo == TipoCampo.Decimal; } }

        partial void OnSeleccionChanged(object? value)
        {
            Error = null;
            AlSeleccionar?.Invoke(value);
        }

        partial void OnTextoChanged(string value) { Error = null; AlEscribir?.Invoke(value); }
        partial void OnFechaChanged(DateTime? value) { Error = null; }

        // ---------- Lectura de valores ----------

        public string ValorTexto { get { return (Texto ?? string.Empty).Trim(); } }

        public int ValorEntero
        {
            get { return int.TryParse(ValorTexto.Replace(".", "").Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0; }
        }

        /// <summary>Dinero: "1.750.905" o "1750905,50" -> 1750905.50 (el punto separa miles, la coma decimales).</summary>
        public decimal ValorMoneda
        {
            get
            {
                string t = ValorTexto.Replace("$", "").Replace(" ", "").Replace(".", "").Replace(',', '.');
                return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
            }
        }

        /// <summary>Numero con decimales con punto o coma: "5,5" o "-10" (porcentajes).</summary>
        public decimal ValorDecimal
        {
            get
            {
                string t = ValorTexto.Replace(" ", "").Replace(',', '.');
                return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
            }
        }

        public T? Elegido<T>() where T : class { return Seleccion as T; }

        /// <summary>Validacion de interfaz: obligatorio y formato. Devuelve false y deja el mensaje en Error.</summary>
        public bool Validar()
        {
            Error = null;
            if (!Visible) return true;
            switch (Tipo)
            {
                case TipoCampo.Fecha:
                    if (Requerido && Fecha == null) Error = "Elige una fecha.";
                    break;
                case TipoCampo.Lista:
                case TipoCampo.ListaPredictiva:
                    if (Requerido && Seleccion == null)
                        Error = ValorTexto.Length > 0 ? "Elige una opción de la lista (no se reconoce lo escrito)." : "Elige una opción.";
                    break;
                default:
                    if (Requerido && ValorTexto.Length == 0) { Error = "Este campo es obligatorio."; break; }
                    if (ValorTexto.Length > MaxLongitud) { Error = "Máximo " + MaxLongitud + " caracteres."; break; }
                    if (ValorTexto.Length > 0 && (Tipo == TipoCampo.Entero || Tipo == TipoCampo.Moneda || Tipo == TipoCampo.Decimal))
                    {
                        string limpio = ValorTexto.Replace(".", "").Replace(",", "").Replace("-", "");
                        if (limpio.Length == 0 || !long.TryParse(limpio, out _)) { Error = "Escribe solo números."; break; }
                        if (Tipo == TipoCampo.Entero && (ValorTexto.Contains(',') || ValorTexto.StartsWith("-")))
                            Error = "Escribe un número entero sin decimales.";
                        if (!PermiteNegativos && ValorTexto.StartsWith("-")) Error = "No se permiten valores negativos.";
                    }
                    break;
            }
            return Error == null;
        }
    }

    /// <summary>ViewModel de la ventana generica de crear/editar.</summary>
    public partial class FormularioViewModel : ObservableObject
    {
        private readonly Func<FormularioViewModel, Task> _guardar;

        public string Titulo { get; }
        public string TextoBoton { get; init; } = "Guardar";
        public ObservableCollection<CampoFormulario> Campos { get; } = new ObservableCollection<CampoFormulario>();

        [ObservableProperty] private string? _mensajeError;
        [ObservableProperty] private string? _advertencia;
        [ObservableProperty] private bool _guardando;

        public FormularioViewModel(string titulo, Func<FormularioViewModel, Task> guardar)
        {
            Titulo = titulo;
            _guardar = guardar;
        }

        public CampoFormulario Agregar(CampoFormulario campo)
        {
            Campos.Add(campo);
            return campo;
        }

        public CampoFormulario this[string clave]
        {
            get
            {
                foreach (CampoFormulario c in Campos) if (c.Clave == clave) return c;
                throw new ArgumentException("No existe el campo " + clave);
            }
        }

        [RelayCommand]
        private async Task Guardar()
        {
            MensajeError = null;
            bool valido = true;
            foreach (CampoFormulario c in Campos) valido &= c.Validar();
            if (!valido)
            {
                MensajeError = "Revisa los campos marcados.";
                return;
            }

            try
            {
                Guardando = true;
                await _guardar(this);
                DialogHost.Close(Notificador.Host, true);
            }
            catch (ReglaDeNegocioException ex)
            {
                MensajeError = ex.Message;   // el formulario queda abierto para corregir
            }
            catch (Exception ex)
            {
                MensajeError = "Ocurrió un error inesperado: " + ex.Message;
            }
            finally
            {
                Guardando = false;
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            DialogHost.Close(Notificador.Host, false);
        }
    }

    public partial class ConfirmacionViewModel : ObservableObject
    {
        public string Titulo { get; }
        public string Mensaje { get; }
        public string TextoBoton { get; }

        public ConfirmacionViewModel(string titulo, string mensaje, string boton)
        {
            Titulo = titulo;
            Mensaje = mensaje;
            TextoBoton = boton;
        }
    }
}
