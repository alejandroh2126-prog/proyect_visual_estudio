using System;
using System.Drawing;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>
    /// Perfil que un trabajador decidió compartir. Es solo de lectura:
    /// el número de documento y el teléfono (privados) nunca aparecen aquí.
    /// </summary>
    public class DialogoPerfilCandidato : Form
    {
        private readonly PictureBox _foto = new PictureBox { Width = 130, Height = 130, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 16, 0) };

        public DialogoPerfilCandidato(FabricaServicios fabrica, CandidatoPublico c)
        {
            Text = "Perfil de " + c.Nombre;
            Font = Tema.Normal;
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(680, 540);

            Label nombre = new Label { Text = c.Nombre, Font = Tema.Titulo, ForeColor = Tema.VerdeOscuro, AutoSize = true };
            Label datos = new Label { Text = "Ciudad: " + c.Ciudad + "\nPerfil profesional: " + c.Profesion, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
            Label experiencia = new Label { Text = "Experiencia y habilidades:\n" + (c.Experiencia == "" ? "No indicó." : c.Experiencia), AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(0, 4, 0, 8) };
            Button cv = Tema.Boton("Abrir hoja de vida", Tema.Verde);
            cv.Enabled = c.TieneHojaVida;
            cv.Click += delegate { Mensajes.Ejecutar(() => Mensajes.Abrir(fabrica.Empleos.RutaCvCandidato(c.UsuarioId))); };

            FlowLayoutPanel texto = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            texto.Controls.AddRange(new Control[] { nombre, datos, experiencia, cv });

            DataGridView tabla = Tema.Tabla("Empresa", "Cargo", "Desde", "Hasta", "Motivo declarado");
            Button cerrar = Tema.Boton("Cerrar", Tema.Gris);
            cerrar.Click += delegate { Close(); };

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Padding = new Padding(18);
            raiz.Fila(Tema.Barra(_foto, texto), false);
            raiz.Fila(Tema.Etiqueta("Experiencia laboral declarada por el trabajador"), false);
            raiz.Fila(tabla, true);
            raiz.Fila(Tema.Barra(cerrar), false);
            Controls.Add(raiz);

            Mensajes.Ejecutar(delegate
            {
                if (c.TieneFoto) ArchivosVista.MostrarFoto(_foto, fabrica.Empleos.RutaFotoCandidato(c.UsuarioId));
                foreach (ExperienciaLaboral e in fabrica.HistorialLaboral.ListarDeCandidato(c.UsuarioId))
                    tabla.AgregarFila(e, e.Empresa, e.Cargo, Formato.Fecha(e.Inicio), Formato.Fecha(e.Fin), e.Motivo);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _foto.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}