using System;
using System.Windows.Forms;
using Entidades;
using Logica;

namespace Presentacion.Comun
{
    /// <summary>
    /// PILAR: HERENCIA + POLIMORFISMO. Todas las pestanas (Inventario, Ventas, Nomina...) heredan de aqui.
    /// La ventana principal las trata a todas igual: llama Mostrar() y cada una sabe recargarse a su manera.
    /// BAJO ACOPLAMIENTO: la vista solo conoce la FabricaServicios, nunca los archivos CSV.
    /// </summary>
    public abstract class VistaBase : UserControl
    {
        protected FabricaServicios Fabrica { get; }
        protected Emprendimiento? Emprendimiento { get; private set; }

        /// <summary>Se dispara cuando la vista cambia datos que otras partes de la ventana deben refrescar.</summary>
        public event EventHandler? DatosCambiaron;

        protected VistaBase(FabricaServicios fabrica)
        {
            Fabrica = fabrica;
            Dock = DockStyle.Fill;
            BackColor = Tema.Fondo;
            Padding = new Padding(20);
            Font = Tema.Normal;
        }

        public void Mostrar(Emprendimiento? emprendimiento)
        {
            Emprendimiento = emprendimiento;
            Recargar();
        }

        protected abstract void Recargar();

        protected void NotificarCambio()
        {
            DatosCambiaron?.Invoke(this, EventArgs.Empty);
        }

        protected bool ExigirEmprendimiento()
        {
            if (Emprendimiento != null) return true;
            Mensajes.Info("Primero crea o selecciona un emprendimiento.");
            return false;
        }
    }
}