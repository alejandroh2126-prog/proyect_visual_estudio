using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;
namespace Presentacion.Vistas
{
    public class VistaEmpleoTrabajador : VistaBase
    {
        private readonly TextBox _ciudad = new TextBox(), _telefono = new TextBox(), _profesion = new TextBox(), _experiencia = new TextBox(), _numero = new TextBox();
        private readonly ComboBox _tipo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _visible = new CheckBox { Text = "Compartir perfil y hoja de vida con emprendedores de esta instalación", AutoSize = true };
        private readonly DataGridView _vacantes = Tema.Tabla("Empresa", "Cargo", "Ciudad", "Salario", "Descripción");
        private readonly DataGridView _postulaciones = Tema.Tabla("Empresa", "Cargo", "Fecha", "Salario mensual", "Estado");
        private readonly Label _cv = Tema.Etiqueta("");
        private readonly TextBox _buscar = new TextBox { Width = 220 };
        public VistaEmpleoTrabajador(FabricaServicios fabrica) : base(fabrica)
        {
            _tipo.Items.AddRange(TiposDocumento.Valores); _tipo.SelectedIndex = 0;
            TableLayoutPanel perfil = Tema.ColumnaVertical();
            perfil.Fila(Tema.TituloVista("Mi perfil profesional"), false);
            perfil.Fila(Tema.Etiqueta("La identificación y el teléfono son privados. La hoja de vida puede contener datos personales; comparte solo lo necesario."), false);
            perfil.Fila(Campo("Ciudad *", _ciudad), false); perfil.Fila(Campo("Teléfono privado", _telefono), false);
            perfil.Fila(Campo("Profesión / cargo buscado *", _profesion), false); perfil.Fila(Campo("Experiencia y habilidades", _experiencia), false);
            perfil.Fila(Campo("Tipo de identificación", _tipo), false); perfil.Fila(Campo("Número de identificación (privado)", _numero), false);
            perfil.Fila(BarraIdentificacion("Identificación frente (privada)", false), false);
            perfil.Fila(BarraIdentificacion("Identificación reverso (privada)", true), false);
            perfil.Fila(_visible, false);
            Button guardar = Tema.Boton("Guardar perfil", Tema.Verde), subir = Tema.Boton("Subir CV (PDF)", Tema.Verde), abrir = Tema.Boton("Ver mi CV", Tema.Gris), quitar = Tema.Boton("Quitar CV", Tema.Rojo);
            guardar.Click += (_, _) => Mensajes.Ejecutar(() => { Fabrica.Empleos.GuardarMiPerfil(new PerfilCandidato { Ciudad = _ciudad.Text, Telefono = _telefono.Text, Profesion = _profesion.Text, Experiencia = _experiencia.Text, TipoDocumento = _tipo.Text, NumeroDocumento = _numero.Text, Visible = _visible.Checked }); Mensajes.Info("Perfil guardado."); });
            subir.Click += (_, _) => Mensajes.Ejecutar(() => { string? r = ArchivosVista.Elegir(false, true); if (r != null) { Fabrica.Empleos.CambiarMiCv(r); ActualizarCv(); } });
            abrir.Click += (_, _) => Mensajes.Ejecutar(() => Mensajes.Abrir(Fabrica.Empleos.RutaMiCv()));
            quitar.Click += (_, _) => { if (Mensajes.Confirmar("¿Quitar la hoja de vida?")) Mensajes.Ejecutar(() => { Fabrica.Empleos.CambiarMiCv(null); ActualizarCv(); }); };
            perfil.Fila(Tema.Barra(guardar, subir, abrir, quitar, _cv), false);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; perfil.Dock = DockStyle.Top; perfil.AutoSize = true; scroll.Controls.Add(perfil);
            TableLayoutPanel ofertas = Tema.ColumnaVertical(); Button actualizar = Tema.Boton("Actualizar", Tema.Gris), postular = Tema.Boton("Postularme", Tema.Verde);
            actualizar.Click += (_, _) => CargarListas(); postular.Click += (_, _) => Mensajes.Ejecutar(() => { Vacante? v = _vacantes.Seleccionado<Vacante>(); if (v == null) throw new ReglaDeNegocioException("Selecciona una vacante."); Fabrica.Empleos.Postular(v.Id); CargarListas(); Mensajes.Info("Postulación registrada."); });
            ofertas.Fila(Tema.Barra(Tema.Etiqueta("Buscar empresa, cargo o ciudad:"), _buscar, actualizar, postular), false);
            _buscar.TextChanged += (_, _) => CargarListas(); ofertas.Fila(_vacantes, true);
            TableLayoutPanel mis = Tema.ColumnaVertical(); Button refrescar = Tema.Boton("Actualizar", Tema.Gris), aceptar = Tema.Boton("Aceptar oferta", Tema.Verde), rechazar = Tema.Boton("Rechazar oferta", Tema.Rojo);
            refrescar.Click += (_, _) => CargarListas(); aceptar.Click += (_, _) => Responder(true); rechazar.Click += (_, _) => Responder(false);
            mis.Fila(Tema.Barra(refrescar, aceptar, rechazar), false); mis.Fila(_postulaciones, true);
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(new TabPage("Perfil") { Controls = { scroll } }); tabs.TabPages.Add(new TabPage("Buscar trabajo") { Controls = { ofertas } }); tabs.TabPages.Add(new TabPage("Mis postulaciones") { Controls = { mis } });
            tabs.SelectedIndexChanged += (_, _) => CargarListas(); Controls.Add(tabs);
        }
        private Control BarraIdentificacion(string titulo, bool reverso)
        {
            Button subir = Tema.Boton("Subir", Tema.Verde), ver = Tema.Boton("Ver", Tema.Gris), quitar = Tema.Boton("Quitar", Tema.Rojo);
            subir.Click += (_, _) => Mensajes.Ejecutar(() => { string? r = ArchivosVista.Elegir(true, true); if (r != null) { Fabrica.Empleos.CambiarMiIdentificacion(reverso, r); Mensajes.Info("Documento privado guardado."); } });
            ver.Click += (_, _) => Mensajes.Ejecutar(() => Mensajes.Abrir(Fabrica.Empleos.RutaMiIdentificacion(reverso)));
            quitar.Click += (_, _) => { if (Mensajes.Confirmar("¿Quitar el documento privado?")) Mensajes.Ejecutar(() => Fabrica.Empleos.CambiarMiIdentificacion(reverso, null)); };
            return Tema.Barra(Tema.Etiqueta(titulo), subir, ver, quitar);
        }
        private void Responder(bool aceptar)
        {
            Mensajes.Ejecutar(() => { Postulacion? p = _postulaciones.Seleccionado<Postulacion>(); if (p == null) throw new ReglaDeNegocioException("Selecciona una oferta."); if (Mensajes.Confirmar(aceptar ? "¿Aceptar y registrar la vinculación en SGAPE?" : "¿Rechazar la oferta?")) { Fabrica.Empleos.ResponderOferta(p.Id, aceptar); CargarListas(); } });
        }
        private static Control Campo(string nombre, Control c) { c.Width = 360; return Tema.Barra(Tema.Etiqueta(nombre), c); }
        private void ActualizarCv() => _cv.Text = Fabrica.Empleos.ObtenerMiPerfil().HojaVida == "" ? "Sin CV" : "CV guardado";
        protected override void Recargar()
        {
            Mensajes.Ejecutar(() => { PerfilCandidato p = Fabrica.Empleos.ObtenerMiPerfil(); _ciudad.Text = p.Ciudad; _telefono.Text = p.Telefono; _profesion.Text = p.Profesion; _experiencia.Text = p.Experiencia; _tipo.SelectedItem = p.TipoDocumento; _numero.Text = p.NumeroDocumento; _visible.Checked = p.Visible; ActualizarCv(); });
            CargarListas();
        }
        private void CargarListas()
        {
            Mensajes.Ejecutar(() => { _vacantes.Rows.Clear(); _postulaciones.Rows.Clear(); foreach (Vacante v in Fabrica.Empleos.ListarVacantesPublicas()) if (Formato.Contiene(v.Empresa + " " + v.Cargo + " " + v.Ciudad, _buscar.Text)) _vacantes.AgregarFila(v, v.Empresa, v.Cargo, v.Ciudad, v.Salario == 0 ? "A convenir" : Formato.Moneda(v.Salario), v.Descripcion); foreach (Postulacion p in Fabrica.Empleos.ListarMisPostulaciones()) _postulaciones.AgregarFila(p, p.Empresa, p.Cargo, Formato.Fecha(p.Fecha), p.SalarioPropuesto == 0 ? "A convenir" : Formato.Moneda(p.SalarioPropuesto), p.Estado); });
        }
    }
}
