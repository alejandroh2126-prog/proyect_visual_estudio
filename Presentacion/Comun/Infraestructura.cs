using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Entidades;
using Logica.Utilidades;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace Presentacion.Comun
{
    /// <summary>Quien inicio sesion y que emprendimiento esta elegido (solo existe en la aplicacion).</summary>
    public static class Sesion
    {
        public static Usuario? Usuario { get; set; }
        public static Emprendimiento? Emprendimiento { get; set; }

        public static int IdUsuario
        {
            get { return Usuario?.Id ?? throw new ReglaDeNegocioException("Debes iniciar sesión."); }
        }

        public static int IdEmprendimiento
        {
            get { return Emprendimiento?.Id ?? throw new ReglaDeNegocioException("Primero crea o elige un emprendimiento."); }
        }

        public static bool HayEmprendimiento { get { return Emprendimiento != null; } }

        public static void Cerrar()
        {
            Usuario = null;
            Emprendimiento = null;
        }
    }

    /// <summary>
    /// Ejecuta trabajo de base de datos fuera de la ventana (para que no se congele).
    /// Las excepciones se ATRAPAN dentro del hilo de fondo y se vuelven a lanzar al esperar: asi Visual Studio
    /// no se detiene en el codigo con "excepcion no controlada por el usuario" cuando una regla de negocio
    /// (por ejemplo, una cedula mal escrita) falla. El usuario solo ve el mensaje.
    /// </summary>
    public static class Fondo
    {
        public static async Task Run(Action accion)
        {
            ExceptionDispatchInfo? error = null;
            await Task.Run(() =>
            {
                try { accion(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            });
            error?.Throw();
        }

        public static async Task<T> Run<T>(Func<T> funcion)
        {
            ExceptionDispatchInfo? error = null;
            T resultado = default!;
            await Task.Run(() =>
            {
                try { resultado = funcion(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            });
            error?.Throw();
            return resultado;
        }
    }

    /// <summary>Mensajes cortos (Snackbar) y dialogos de confirmacion.</summary>
    public static class Notificador
    {
        public const string Host = "Raiz";

        public static SnackbarMessageQueue Cola { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(6));

        public static void Exito(string mensaje) { Cola.Enqueue(mensaje); }

        public static void Error(string mensaje)
        {
            Cola.Enqueue(mensaje, "OK", () => { });
        }

        /// <summary>Muestra un formulario en el DialogHost. Devuelve true si se guardo.</summary>
        public static async Task<bool> Formulario(FormularioViewModel formulario)
        {
            if (DialogHost.IsDialogOpen(Host)) return false;
            object? resultado = await DialogHost.Show(formulario, Host);
            return resultado is bool b && b;
        }

        public static async Task<bool> Confirmar(string titulo, string mensaje, string boton = "Sí, continuar")
        {
            if (DialogHost.IsDialogOpen(Host)) return false;
            object? resultado = await DialogHost.Show(new ConfirmacionViewModel(titulo, mensaje, boton), Host);
            return resultado is bool b && b;
        }
    }

    /// <summary>Guardar y abrir archivos que el usuario descarga (PDF, hojas de vida).</summary>
    public static class Archivos
    {
        public static string? GuardarComo(string nombreSugerido, byte[] contenido, string filtro)
        {
            SaveFileDialog dialogo = new SaveFileDialog
            {
                FileName = nombreSugerido,
                Filter = filtro,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true
            };
            if (dialogo.ShowDialog() != true) return null;
            File.WriteAllBytes(dialogo.FileName, contenido);
            return dialogo.FileName;
        }

        public static void Abrir(string ruta)
        {
            try { Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true }); }
            catch (Exception) { Notificador.Error("El archivo se guardó, pero no hay un programa para abrirlo."); }
        }
    }

    /// <summary>Base de todos los ViewModels: indicador de carga y manejo de errores en un solo lugar.</summary>
    public abstract partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _ocupado;

        /// <summary>Ejecuta una tarea mostrando la barra de carga; los errores se muestran al usuario.</summary>
        protected async Task Ejecutar(Func<Task> accion)
        {
            try
            {
                Ocupado = true;
                await accion();
            }
            catch (ReglaDeNegocioException ex)
            {
                Notificador.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Notificador.Error("Ocurrió un error inesperado: " + ex.Message);
            }
            finally
            {
                Ocupado = false;
            }
        }

        /// <summary>Se llama cada vez que se entra a la seccion o cambia el emprendimiento.</summary>
        public virtual Task Cargar() { return Task.CompletedTask; }
    }

    // =====================================================================
    // Convertidores para la vista
    // =====================================================================

    public class MonedaConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) { return value is decimal d ? Formato.Moneda(d) : string.Empty; }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    public class PorcentajeConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) { return value is decimal d ? Formato.Porcentaje(d) : string.Empty; }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    public class FechaConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is DateTime d) return (p as string) == "hora" ? Formato.FechaHora(d) : Formato.Fecha(d);
            return string.Empty;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    public class TamanoConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) { return value is long l ? Formato.Tamano(l) : string.Empty; }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    public class EstadoPostulacionConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) { return Formato.EstadoPostulacion(value as string ?? string.Empty); }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    /// <summary>OK / activo / abierta -> verde; ALERTA / en_revision -> naranja; EXCEDIDO / inactivo -> rojo.</summary>
    public class EstadoAColorConverter : IValueConverter
    {
        private static readonly Brush Verde = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly Brush Naranja = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
        private static readonly Brush Rojo = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly Brush Gris = new SolidColorBrush(Color.FromRgb(0x61, 0x61, 0x61));

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is bool b) return b ? Rojo : Verde;   // por ejemplo StockBajo
            if (value is decimal d) return d < 0 ? Rojo : Verde;
            switch ((value as string ?? string.Empty).ToLowerInvariant())
            {
                case "ok": case "activo": case "abierta": case "aceptada": case "recibida": return Verde;
                case "alerta": case "en_revision": return Naranja;
                case "excedido": case "inactivo": case "rechazada": return Rojo;
                default: return Gris;
            }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    public class NuloAVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool hayValor = value != null && !(value is string s && s.Length == 0);
            if ((p as string) == "invertir") hayValor = !hayValor;
            return hayValor ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    /// <summary>Visible si los dos valores son iguales (marca con un check el color elegido en Apariencia).</summary>
    public class IgualesAVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type t, object p, CultureInfo c)
        {
            if (values.Length < 2) return Visibility.Collapsed;
            string a = values[0]?.ToString() ?? string.Empty, b = values[1]?.ToString() ?? string.Empty;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    /// <summary>true -> oculto; false -> visible.</summary>
    public class BoolAVisibleInvertidoConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) { return value is bool b && b ? Visibility.Collapsed : Visibility.Visible; }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    /// <summary>Muestra el control del formulario que corresponde al tipo de campo (parametro: "Texto,Numero").</summary>
    public class TipoCampoAVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            string tipo = value?.ToString() ?? string.Empty;
            foreach (string opcion in (p as string ?? string.Empty).Split(','))
                if (opcion.Trim() == tipo) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
    }

    // =====================================================================
    // Comportamiento: solo numeros en un TextBox (validacion en la interfaz)
    // =====================================================================

    public static class SoloNumeros
    {
        public static readonly DependencyProperty ActivoProperty = DependencyProperty.RegisterAttached(
            "Activo", typeof(bool), typeof(SoloNumeros), new PropertyMetadata(false, AlCambiar));

        public static bool GetActivo(DependencyObject o) { return (bool)o.GetValue(ActivoProperty); }
        public static void SetActivo(DependencyObject o, bool valor) { o.SetValue(ActivoProperty, valor); }

        /// <summary>Permite negativos (para porcentajes).</summary>
        public static readonly DependencyProperty NegativosProperty = DependencyProperty.RegisterAttached(
            "Negativos", typeof(bool), typeof(SoloNumeros), new PropertyMetadata(false));

        public static bool GetNegativos(DependencyObject o) { return (bool)o.GetValue(NegativosProperty); }
        public static void SetNegativos(DependencyObject o, bool valor) { o.SetValue(NegativosProperty, valor); }

        private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox caja) return;
            caja.PreviewTextInput -= Filtrar;
            DataObject.RemovePastingHandler(caja, AlPegar);
            if ((bool)e.NewValue)
            {
                caja.PreviewTextInput += Filtrar;
                DataObject.AddPastingHandler(caja, AlPegar);
            }
        }

        private static bool Valido(TextBox caja, string texto)
        {
            bool negativos = GetNegativos(caja);
            foreach (char ch in texto)
                if (!char.IsDigit(ch) && ch != '.' && ch != ',' && !(negativos && ch == '-')) return false;
            return true;
        }

        private static void Filtrar(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Valido((TextBox)sender, e.Text);
        }

        private static void AlPegar(object sender, DataObjectPastingEventArgs e)
        {
            string texto = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
            if (!Valido((TextBox)sender, texto)) e.CancelCommand();
        }
    }
}
