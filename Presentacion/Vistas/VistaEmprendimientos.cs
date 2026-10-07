using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;

namespace Presentacion.Vistas
{
    /// <summary>CRUD de emprendimientos: crear, ver, editar y eliminar.</summary>
    public class VistaEmprendimientos : VistaBase
    {
        private readonly DataGridView _tabla = Tema.Tabla("Nombre", "Sector", "Inicio", "Estado", "Descripción");

        public VistaEmprendimientos(FabricaServicios fabrica) : base(fabrica)
        {
            Button nuevo = Tema.Boton("Nuevo emprendimiento", Tema.Verde);
            Button editar = Tema.Boton("Editar", Tema.Gris);
            Button eliminar = Tema.Boton("Eliminar", Tema.Rojo);
            nuevo.Click += delegate { Nuevo(); };
            editar.Click += delegate { Editar(); };
            eliminar.Click += delegate { Eliminar(); };
            _tabla.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Editar(); };

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Mis emprendimientos"), false);
            raiz.Fila(Tema.Barra(nuevo, editar, eliminar), false);
            raiz.Fila(_tabla, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _tabla.Rows.Clear();
            Mensajes.Ejecutar(delegate
            {
                foreach (Emprendimiento e in Fabrica.Emprendimientos.ListarDelUsuario())
                {
                    DataGridViewRow fila = _tabla.AgregarFila(e, e.Nombre, e.Sector, Formato.Fecha(e.FechaInicio),
                        e.Estado, e.Descripcion);
                    if (e.Estado == Estados.Inactivo) fila.DefaultCellStyle.ForeColor = Tema.TextoSuave;
                }
            });
        }

        private void Nuevo()
        {
            using (DialogoEmprendimiento d = new DialogoEmprendimiento(Fabrica, null))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Cambio();
            }
        }

        private void Editar()
        {
            Emprendimiento? e = _tabla.Seleccionado<Emprendimiento>();
            if (e == null) { Mensajes.Info("Selecciona un emprendimiento de la lista."); return; }
            using (DialogoEmprendimiento d = new DialogoEmprendimiento(Fabrica, e))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Cambio();
            }
        }

        private void Eliminar()
        {
            Emprendimiento? e = _tabla.Seleccionado<Emprendimiento>();
            if (e == null) { Mensajes.Info("Selecciona un emprendimiento de la lista."); return; }
            if (!Mensajes.Confirmar("¿Eliminar '" + e.Nombre + "' y TODOS sus datos (trabajadores, inventario, ventas y movimientos)?\nEsta acción no se puede deshacer."))
                return;
            if (Mensajes.Ejecutar(delegate { Fabrica.Emprendimientos.Eliminar(e.Id); })) Cambio();
        }

        private void Cambio()
        {
            Recargar();
            NotificarCambio();
        }
    }
}