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
    public class VistaEmpleoEmprendedor : VistaBase
    {
        private readonly DataGridView _vacantes = Tema.Tabla("Cargo", "Ciudad", "Salario", "Estado", "Descripción");
        private readonly DataGridView _candidatos = Tema.Tabla("Nombre", "Ciudad", "Perfil profesional", "Experiencia", "CV");
        private readonly DataGridView _historial = Tema.Tabla("Empresa", "Trabajador", "Cargo", "Inicio", "Fin", "Motivo", "Revisión");
        private readonly DataGridView _postulaciones = Tema.Tabla("Candidato", "Cargo", "Fecha", "Estado", "Motivo / revisión");
        public VistaEmpleoEmprendedor(FabricaServicios fabrica) : base(fabrica)
        {
            Button nueva = Tema.Boton("Publicar vacante", Tema.Verde), cerrar = Tema.Boton("Cerrar vacante", Tema.Gris), ver = Tema.Boton("Ver CV", Tema.Verde), actualizar = Tema.Boton("Actualizar", Tema.Gris), verPost = Tema.Boton("Ver CV del candidato", Tema.Verde);
            nueva.Click += (_, _) => { if (ExigirEmprendimiento()) Mensajes.Ejecutar(() => { using DialogoVacante d = new DialogoVacante(Fabrica, Emprendimiento!.Id); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); }); };
            cerrar.Click += (_, _) => Mensajes.Ejecutar(() => { Vacante? v = _vacantes.Seleccionado<Vacante>(); if (v == null) throw new ReglaDeNegocioException("Selecciona una vacante."); if (Mensajes.Confirmar("¿Cerrar esta vacante?")) { Fabrica.Empleos.CerrarVacante(v.Id); Recargar(); } });
            ver.Click += (_, _) => Mensajes.Ejecutar(() => { CandidatoPublico? c = _candidatos.Seleccionado<CandidatoPublico>(); if (c == null) throw new ReglaDeNegocioException("Selecciona un candidato."); Mensajes.Abrir(Fabrica.Empleos.RutaCvCandidato(c.UsuarioId)); });
            actualizar.Click += (_, _) => Recargar();
            verPost.Click += (_, _) => Mensajes.Ejecutar(() => { Postulacion p = Seleccion(); Mensajes.Abrir(Fabrica.Empleos.RutaCvCandidato(p.TrabajadorUsuarioId)); });
            Button ofertar = Tema.Boton("Ofertar contratación", Tema.Verde), rechazar = Tema.Boton("Rechazar", Tema.Rojo), finalizar = Tema.Boton("Finalizar vínculo", Tema.Gris), revision = Tema.Boton("Revisar solicitud", Tema.Gris);
            ofertar.Click += (_, _) => Mensajes.Ejecutar(() => { Postulacion p = Seleccion(); if (Mensajes.Confirmar("¿Enviar una propuesta de contratación?")) { Fabrica.Empleos.Ofertar(p.Id); Recargar(); } });
            rechazar.Click += (_, _) => Mensajes.Ejecutar(() => { Postulacion p = Seleccion(); if (Mensajes.Confirmar("¿Rechazar esta postulación?")) { Fabrica.Empleos.Rechazar(p.Id); Recargar(); } });
            finalizar.Click += (_, _) => Mensajes.Ejecutar(() => { using DialogoFinalizarEmpleo d = new DialogoFinalizarEmpleo(Fabrica, Seleccion()); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); });
            revision.Click += (_, _) => Mensajes.Ejecutar(() => { using DialogoResolverRevision d = new DialogoResolverRevision(Fabrica, Seleccion()); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); });
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(Pagina("Vacantes", _vacantes, nueva, cerrar)); tabs.TabPages.Add(Pagina("Buscar trabajadores", _candidatos, ver, actualizar)); tabs.TabPages.Add(Pagina("Postulaciones", _postulaciones, verPost, ofertar, rechazar, finalizar, revision));
            Button revisarHistorico = Tema.Boton("Revisar solicitud", Tema.Gris);
            revisarHistorico.Click += (_, _) => Mensajes.Ejecutar(() => { Postulacion? p = _historial.Seleccionado<Postulacion>(); if (p == null) throw new ReglaDeNegocioException("Selecciona una vinculación finalizada."); using DialogoResolverRevision d = new DialogoResolverRevision(Fabrica, p); if (d.ShowDialog(this) == DialogResult.OK) Recargar(); });
            tabs.TabPages.Add(Pagina("Historial de mis empresas", _historial, revisarHistorico));
            tabs.SelectedIndexChanged += (_, _) => Recargar(); Controls.Add(tabs);
        }
        private static TabPage Pagina(string titulo, DataGridView tabla, params Control[] botones) { TableLayoutPanel p = Tema.ColumnaVertical(); p.Fila(Tema.Barra(botones), false); p.Fila(tabla, true); TabPage t = new TabPage(titulo); t.Controls.Add(p); return t; }
        private Postulacion Seleccion() => _postulaciones.Seleccionado<Postulacion>() ?? throw new ReglaDeNegocioException("Selecciona una postulación.");
        protected override void Recargar()
        {
            Mensajes.Ejecutar(() =>
            {
                _vacantes.Rows.Clear(); _candidatos.Rows.Clear(); _postulaciones.Rows.Clear();
                List<CandidatoPublico> candidatos = Fabrica.Empleos.ListarCandidatos();
                foreach (CandidatoPublico c in candidatos) _candidatos.AgregarFila(c, c.Nombre, c.Ciudad, c.Profesion, c.Experiencia, c.TieneHojaVida ? "Sí" : "No");
                _historial.Rows.Clear();
                foreach (Postulacion p in Fabrica.Empleos.ListarHistorialEmprendedor()) _historial.AgregarFila(p, p.Empresa, p.NombreTrabajador, p.Cargo, p.InicioLaboral.HasValue ? Formato.Fecha(p.InicioLaboral.Value) : "—", p.FinLaboral.HasValue ? Formato.Fecha(p.FinLaboral.Value) : "—", p.MotivoRetiro, p.SolicitudRevision == "" ? "—" : p.RespuestaRevision == "" ? "Pendiente" : p.RespuestaRevision);
                if (Emprendimiento == null) return;
                foreach (Vacante v in Fabrica.Empleos.ListarVacantesPropias(Emprendimiento.Id)) _vacantes.AgregarFila(v, v.Cargo, v.Ciudad, v.Salario == 0 ? "A convenir" : Formato.Moneda(v.Salario), v.Abierta ? "Abierta" : "Cerrada", v.Descripcion);
                foreach (Postulacion p in Fabrica.Empleos.ListarPostulacionesEmpresa(Emprendimiento.Id)) _postulaciones.AgregarFila(p, (p.Estado == EstadoPostulacion.Contratada || p.Estado == EstadoPostulacion.Finalizada) && p.NombreTrabajador != "" ? p.NombreTrabajador : candidatos.Find(c => c.UsuarioId == p.TrabajadorUsuarioId)?.Nombre ?? "Perfil no compartido", p.Cargo, Formato.Fecha(p.Fecha), p.Estado, p.MotivoRetiro + (p.SolicitudRevision != "" ? " · " + (p.RespuestaRevision == "" ? "Revisión pendiente" : p.RespuestaRevision) : ""));
            });
        }
    }
}
