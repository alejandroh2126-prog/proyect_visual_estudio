using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;

namespace Presentacion.Vistas
{
    /// <summary>Cargos, trabajadores y resumen de nomina (salud 4% + pension 4%).</summary>
    public class VistaNomina : VistaBase
    {
        private readonly TarjetaKpi _kActivos = new TarjetaKpi("Trabajadores activos", Tema.VerdeOscuro);
        private readonly TarjetaKpi _kBruta = new TarjetaKpi("Nómina mensual bruta", Tema.Gris);
        private readonly TarjetaKpi _kDeducciones = new TarjetaKpi("Deducciones (8%)", Tema.Rojo);
        private readonly TarjetaKpi _kNeta = new TarjetaKpi("Nómina neta", Tema.Verde);
        private readonly DataGridView _tablaCargos = Tema.Tabla("Cargo", "Salario base", "Quincenal", "Deducciones", "Neto", "Descripción");
        private readonly DataGridView _tablaTrabajadores = Tema.Tabla("Trabajador", "Cédula", "Cargo", "Ingreso", "Salario mensual", "Neto", "Estado");

        public VistaNomina(FabricaServicios fabrica) : base(fabrica)
        {
            _tablaCargos.AlinearDerecha(1, 2, 3, 4);
            _tablaTrabajadores.AlinearDerecha(4, 5);

            // ---- pestana Cargos ----
            Button nuevoCargo = Tema.Boton("Nuevo cargo", Tema.Verde);
            Button editarCargo = Tema.Boton("Editar", Tema.Gris);
            Button eliminarCargo = Tema.Boton("Eliminar", Tema.Rojo);
            nuevoCargo.Click += delegate { NuevoCargo(); };
            editarCargo.Click += delegate { EditarCargo(); };
            eliminarCargo.Click += delegate { EliminarCargo(); };
            _tablaCargos.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditarCargo(); };

            TableLayoutPanel panelCargos = Tema.ColumnaVertical();
            panelCargos.Fila(Tema.Barra(nuevoCargo, editarCargo, eliminarCargo), false);
            panelCargos.Fila(_tablaCargos, true);
            TabPage paginaCargos = new TabPage("1. Cargos");
            paginaCargos.Padding = new Padding(12);
            paginaCargos.BackColor = Tema.Fondo;
            paginaCargos.Controls.Add(panelCargos);

            // ---- pestana Trabajadores ----
            Button nuevoTrab = Tema.Boton("Nuevo trabajador", Tema.Verde);
            Button editarTrab = Tema.Boton("Editar", Tema.Gris);
            Button eliminarTrab = Tema.Boton("Eliminar", Tema.Rojo);
            nuevoTrab.Click += delegate { NuevoTrabajador(); };
            editarTrab.Click += delegate { EditarTrabajador(); };
            eliminarTrab.Click += delegate { EliminarTrabajador(); };
            _tablaTrabajadores.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditarTrabajador(); };

            TableLayoutPanel panelTrab = Tema.ColumnaVertical();
            panelTrab.Fila(Tema.Barra(nuevoTrab, editarTrab, eliminarTrab), false);
            panelTrab.Fila(_tablaTrabajadores, true);
            TabPage paginaTrab = new TabPage("2. Trabajadores");
            paginaTrab.Padding = new Padding(12);
            paginaTrab.BackColor = Tema.Fondo;
            paginaTrab.Controls.Add(panelTrab);

            TabControl pestanas = new TabControl();
            pestanas.Font = Tema.Normal;
            pestanas.TabPages.Add(paginaCargos);
            pestanas.TabPages.Add(paginaTrab);

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Nómina"), false);
            raiz.Fila(Tema.FilaTarjetas(_kActivos, _kBruta, _kDeducciones, _kNeta), false);
            raiz.Fila(pestanas, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _tablaCargos.Rows.Clear();
            _tablaTrabajadores.Rows.Clear();

            if (Emprendimiento == null)
            {
                _kActivos.Valor = "0";
                _kBruta.Valor = Formato.Moneda(0);
                _kDeducciones.Valor = Formato.Moneda(0);
                _kNeta.Valor = Formato.Moneda(0);
                return;
            }

            int id = Emprendimiento.Id;
            Mensajes.Ejecutar(delegate
            {
                foreach (Cargo c in Fabrica.Nomina.ListarCargos(id))
                {
                    _tablaCargos.AgregarFila(c, c.Nombre, Formato.Moneda(c.SalarioBase), Formato.Moneda(c.SalarioQuincenal),
                        Formato.Moneda(c.TotalDeducciones), Formato.Moneda(c.SalarioNeto), c.Descripcion);
                }

                foreach (Trabajador t in Fabrica.Nomina.ListarTrabajadores(id))
                {
                    DataGridViewRow fila = _tablaTrabajadores.AgregarFila(t, t.NombreCompleto, t.Cedula, t.Cargo.Nombre,
                        Formato.Fecha(t.FechaIngreso), Formato.Moneda(t.SalarioMensual), Formato.Moneda(t.SalarioNeto), t.Estado);
                    if (!t.EstaActivo) fila.DefaultCellStyle.ForeColor = Tema.TextoSuave;
                }

                ResumenNomina r = Fabrica.Nomina.CalcularResumen(id);
                _kActivos.Valor = r.TrabajadoresActivos.ToString();
                _kBruta.Valor = Formato.Moneda(r.NominaMensualBruta);
                _kDeducciones.Valor = Formato.Moneda(r.TotalDeducciones);
                _kNeta.Valor = Formato.Moneda(r.NominaNeta);
            });
        }

        // ---------------- Cargos ----------------

        private void NuevoCargo()
        {
            if (!ExigirEmprendimiento()) return;
            using (DialogoCargo d = new DialogoCargo(Fabrica, Emprendimiento!.Id, null))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Recargar();
            }
        }

        private void EditarCargo()
        {
            if (!ExigirEmprendimiento()) return;
            Cargo? c = _tablaCargos.Seleccionado<Cargo>();
            if (c == null) { Mensajes.Info("Selecciona un cargo de la lista."); return; }
            using (DialogoCargo d = new DialogoCargo(Fabrica, Emprendimiento!.Id, c))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Recargar();
            }
        }

        private void EliminarCargo()
        {
            Cargo? c = _tablaCargos.Seleccionado<Cargo>();
            if (c == null) { Mensajes.Info("Selecciona un cargo de la lista."); return; }
            if (!Mensajes.Confirmar("¿Eliminar el cargo '" + c.Nombre + "'?")) return;
            if (Mensajes.Ejecutar(delegate { Fabrica.Nomina.EliminarCargo(c.Id); })) Recargar();
        }

        // ---------------- Trabajadores ----------------

        private void NuevoTrabajador()
        {
            if (!ExigirEmprendimiento()) return;
            List<Cargo> cargos = Fabrica.Nomina.ListarCargos(Emprendimiento!.Id);
            if (cargos.Count == 0)
            {
                Mensajes.Info("Primero crea al menos un cargo en la pestaña '1. Cargos'.");
                return;
            }
            using (DialogoTrabajador d = new DialogoTrabajador(Fabrica, Emprendimiento.Id, cargos, null))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Recargar();
            }
        }

        private void EditarTrabajador()
        {
            if (!ExigirEmprendimiento()) return;
            Trabajador? t = _tablaTrabajadores.Seleccionado<Trabajador>();
            if (t == null) { Mensajes.Info("Selecciona un trabajador de la lista."); return; }
            List<Cargo> cargos = Fabrica.Nomina.ListarCargos(Emprendimiento!.Id);
            using (DialogoTrabajador d = new DialogoTrabajador(Fabrica, Emprendimiento.Id, cargos, t))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Recargar();
            }
        }

        private void EliminarTrabajador()
        {
            Trabajador? t = _tablaTrabajadores.Seleccionado<Trabajador>();
            if (t == null) { Mensajes.Info("Selecciona un trabajador de la lista."); return; }
            if (!Mensajes.Confirmar("¿Eliminar a " + t.NombreCompleto + "?")) return;
            if (Mensajes.Ejecutar(delegate { Fabrica.Nomina.EliminarTrabajador(t.Id); })) Recargar();
        }
    }
}