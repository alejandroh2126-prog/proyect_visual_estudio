using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;
namespace Presentacion.Vistas
{
    public class VistaHistorialLaboral : VistaBase
    {
        private readonly DataGridView _registradas = Tema.Tabla("Empresa", "Cargo", "Inicio", "Fin", "Motivo", "Revisión");
        private readonly DataGridView _declaradas = Tema.Tabla("Empresa", "Cargo", "Inicio", "Fin", "Motivo");
        public VistaHistorialLaboral(FabricaServicios fabrica) : base(fabrica)
        {
            Button nueva = Tema.Boton("Agregar experiencia", Tema.Verde), editar = Tema.Boton("Editar", Tema.Gris), eliminar = Tema.Boton("Quitar", Tema.Rojo), revisar = Tema.Boton("Solicitar corrección", Tema.Gris), refrescar = Tema.Boton("Actualizar", Tema.Gris);
            nueva.Click += (_, _) => Editar(null);
            editar.Click += (_, _) => { ExperienciaLaboral? e = _declaradas.Seleccionado<ExperienciaLaboral>(); if (e == null) Mensajes.Info("Selecciona una experiencia."); else Editar(e); };
            eliminar.Click += (_, _) => Mensajes.Ejecutar(() => { ExperienciaLaboral? e = _declaradas.Seleccionado<ExperienciaLaboral>(); if (e == null) throw new ReglaDeNegocioException("Selecciona una experiencia."); if (Mensajes.Confirmar("¿Quitar esta experiencia?")) { Fabrica.HistorialLaboral.Eliminar(e.Id); Recargar(); } });
            revisar.Click += (_, _) => Mensajes.Ejecutar(() => { Postulacion? p = _registradas.Seleccionado<Postulacion>(); if (p == null) throw new ReglaDeNegocioException("Selecciona una vinculación."); using DialogoRevisionHistorial d = new DialogoRevisionHistorial(Fabrica, p); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); });
            refrescar.Click += (_, _) => Recargar();
            TableLayoutPanel raiz = Tema.ColumnaVertical(); raiz.Fila(Tema.TituloVista("Mi historial laboral privado"), false);
            raiz.Fila(Tema.Etiqueta("Vinculaciones registradas en SGAPE (no equivalen a certificación oficial)."), false); raiz.Fila(Tema.Barra(revisar, refrescar), false); raiz.Fila(_registradas, true);
            // Dos tablas con alturas equivalentes.
            raiz.Fila(Tema.Etiqueta("Experiencia anterior declarada por ti"), false); raiz.Fila(Tema.Barra(nueva, editar, eliminar), false); raiz.Fila(_declaradas, true);
            Controls.Add(raiz);
        }
        private void Editar(ExperienciaLaboral? e) => Mensajes.Ejecutar(() => { using DialogoExperiencia d = new DialogoExperiencia(Fabrica, e); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); });
        protected override void Recargar()
        {
            Mensajes.Ejecutar(() => { _registradas.Rows.Clear(); _declaradas.Rows.Clear();
                foreach (Postulacion p in Fabrica.HistorialLaboral.ListarRegistradas()) _registradas.AgregarFila(p, p.Empresa, p.Cargo, p.InicioLaboral.HasValue ? Formato.Fecha(p.InicioLaboral.Value) : "—", p.FinLaboral.HasValue ? Formato.Fecha(p.FinLaboral.Value) : "Actualidad", p.MotivoRetiro, p.SolicitudRevision == "" ? "—" : p.RespuestaRevision == "" ? "Solicitada" : p.RespuestaRevision);
                foreach (ExperienciaLaboral e in Fabrica.HistorialLaboral.ListarDeclaradas()) _declaradas.AgregarFila(e, e.Empresa, e.Cargo, Formato.Fecha(e.Inicio), Formato.Fecha(e.Fin), e.Motivo);
            });
        }
    }
}
