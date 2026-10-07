using System;
using System.Diagnostics;
using System.Windows.Forms;
using Logica.Utilidades;

namespace Presentacion.Comun
{
    /// <summary>
    /// UNA SOLA MISION: hablar con el usuario (cuadros de mensaje) y ejecutar acciones de forma segura.
    /// Ejecutar() evita repetir try/catch en cada boton: los errores de negocio se muestran
    /// con su mensaje y cualquier otro error se muestra sin cerrar el programa.
    /// </summary>
    public static class Mensajes
    {
        private const string Titulo = "SGAPE";

        public static void Info(string mensaje)
        {
            MessageBox.Show(mensaje, Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Error(string mensaje)
        {
            MessageBox.Show(mensaje, Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static bool Confirmar(string pregunta)
        {
            return MessageBox.Show(pregunta, Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        /// <summary>Ejecuta la accion. Devuelve true si salio bien, false si hubo error (ya mostrado).</summary>
        public static bool Ejecutar(Action accion)
        {
            try
            {
                accion();
                return true;
            }
            catch (ReglaDeNegocioException ex)
            {
                Error(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Error("Ocurrió un error inesperado:\n" + ex.Message);
                return false;
            }
        }

        /// <summary>Abre un archivo o carpeta con el programa predeterminado de Windows.</summary>
        public static void Abrir(string ruta)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Error("No se pudo abrir:\n" + ruta + "\n\n" + ex.Message);
            }
        }
    }
}