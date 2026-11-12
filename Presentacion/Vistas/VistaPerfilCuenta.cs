using System;
using System.Drawing;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Vistas
{
    public class VistaPerfilCuenta : VistaBase
    {
        private readonly PictureBox _foto = new PictureBox { Width = 130, Height = 130, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        private readonly Label _nombre = Tema.Etiqueta("");
        private readonly Button _fondo = Tema.Boton("Color de fondo", Tema.Gris);
        private readonly Button _botones = Tema.Boton("Color de botones", Tema.Gris);
        private string _colorFondo = "", _colorBotones = "";
        private string? _fotoNueva;
        private bool _quitar;
        public VistaPerfilCuenta(FabricaServicios fabrica) : base(fabrica)
        {
            Button elegir = Tema.Boton("Elegir foto", Tema.Verde), quitar = Tema.Boton("Quitar foto", Tema.Gris), guardar = Tema.Boton("Guardar cambios", Tema.Verde), defecto = Tema.Boton("Restaurar colores", Tema.Gris);
            elegir.Click += (_, _) => Mensajes.Ejecutar(() => { string? r = ArchivosVista.Elegir(true, false); if (r != null) { ArchivosVista.MostrarFoto(_foto, r); _fotoNueva = r; _quitar = false; } });
            quitar.Click += (_, _) => { _foto.Image?.Dispose(); _foto.Image = null; _fotoNueva = null; _quitar = true; };
            _fondo.Click += (_, _) => ElegirColor(true);
            _botones.Click += (_, _) => ElegirColor(false);
            defecto.Click += (_, _) => { _colorFondo = _colorBotones = ""; Pintar(); };
            guardar.Click += (_, _) => { if (Mensajes.Ejecutar(() => Fabrica.PerfilCuenta.Guardar(_colorFondo, _colorBotones, _fotoNueva, _quitar))) { Recargar(); Mensajes.Info("Perfil guardado. Cierra sesión y vuelve a entrar para aplicar los colores."); } };
            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Mi perfil y apariencia"), false);
            raiz.Fila(_nombre, false); raiz.Fila(Tema.Barra(_foto, elegir, quitar), false);
            raiz.Fila(Tema.Barra(_fondo, _botones, defecto, guardar), false);
            raiz.Fila(Tema.Etiqueta("Foto opcional. Fondo claro y botones oscuros para facilitar la lectura."), false);
            Controls.Add(raiz);
        }
        private void ElegirColor(bool fondo)
        {
            using ColorDialog d = new ColorDialog { FullOpen = true, Color = fondo ? Tema.Fondo : Tema.VerdeOscuro };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            string hex = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
            if (fondo) _colorFondo = hex; else _colorBotones = hex;
            Pintar();
        }
        private void Pintar() { _fondo.Text = "Fondo: " + (_colorFondo == "" ? "original" : _colorFondo); _botones.Text = "Botones: " + (_colorBotones == "" ? "original" : _colorBotones); }
        protected override void Recargar()
        {
            _nombre.Text = Sesion.UsuarioActual?.Nombre + " · " + Sesion.UsuarioActual?.Email;
            Mensajes.Ejecutar(() => { PerfilCuenta p = Fabrica.PerfilCuenta.Obtener(); _colorFondo = p.ColorFondo; _colorBotones = p.ColorBotones; _fotoNueva = null; _quitar = false; _foto.Image?.Dispose(); _foto.Image = null; Pintar(); if (p.Foto != "") ArchivosVista.MostrarFoto(_foto, Fabrica.PerfilCuenta.RutaFoto()); });
        }
        protected override void Dispose(bool disposing) { if (disposing) _foto.Image?.Dispose(); base.Dispose(disposing); }
    }
}
