using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoFichaTrabajador : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _id;
        private readonly TextBox _telefono, _correo, _ciudad, _direccion, _nacionalidad, _estudios, _contrato;
        public DialogoFichaTrabajador(FabricaServicios fabrica, Trabajador t) : base("Ficha de " + t.NombreCompleto)
        {
            _fabrica = fabrica; _id = t.Id;
            FichaTrabajador f = fabrica.FichasTrabajador.Obtener(_id);
            _telefono = Campo("Teléfono", new TextBox { Text = f.Telefono }); _correo = Campo("Correo", new TextBox { Text = f.Correo });
            _ciudad = Campo("Ciudad", new TextBox { Text = f.Ciudad }); _direccion = Campo("Dirección", new TextBox { Text = f.Direccion });
            _nacionalidad = Campo("Nacionalidad", new TextBox { Text = f.Nacionalidad }); _estudios = Campo("Estudios / formación", new TextBox { Text = f.Estudios });
            _contrato = Campo("Tipo de contrato", new TextBox { Text = f.TipoContrato });
            AgregarAdjunto("Foto (fondo blanco)", AdjuntoTrabajador.Foto); AgregarAdjunto("Identificación (frente)", AdjuntoTrabajador.DocumentoFrente); AgregarAdjunto("Identificación (reverso)", AdjuntoTrabajador.DocumentoReverso); AgregarAdjunto("Hoja de vida (PDF)", AdjuntoTrabajador.HojaVida);
            Campo("Privacidad", new Label { Text = "Con autorización del trabajador.\nLos adjuntos se guardan al elegirlos.", AutoSize = true });
        }
        private void AgregarAdjunto(string etiqueta, AdjuntoTrabajador tipo)
        {
            Button subir = Tema.Boton("Subir", Tema.Verde), abrir = Tema.Boton("Ver", Tema.Gris), borrar = Tema.Boton("Quitar", Tema.Rojo);
            subir.Click += (_, _) => Mensajes.Ejecutar(() => { if (!Mensajes.Confirmar("¿Tienes autorización para almacenar este documento?")) return; string? r = ArchivosVista.Elegir(tipo != AdjuntoTrabajador.HojaVida, tipo != AdjuntoTrabajador.Foto); if (r != null) { _fabrica.FichasTrabajador.CambiarAdjunto(_id, tipo, r); Mensajes.Info("Documento guardado."); } });
            abrir.Click += (_, _) => Mensajes.Ejecutar(() => Mensajes.Abrir(_fabrica.FichasTrabajador.RutaAdjunto(_id, tipo)));
            borrar.Click += (_, _) => { if (Mensajes.Confirmar("¿Quitar el documento?")) Mensajes.Ejecutar(() => _fabrica.FichasTrabajador.CambiarAdjunto(_id, tipo, null)); };
            Campo(etiqueta, Tema.Barra(subir, abrir, borrar));
        }
        protected override void Aceptar() => _fabrica.FichasTrabajador.Guardar(_id, new FichaTrabajador { Telefono = _telefono.Text, Correo = _correo.Text, Ciudad = _ciudad.Text, Direccion = _direccion.Text, Nacionalidad = _nacionalidad.Text, Estudios = _estudios.Text, TipoContrato = _contrato.Text });
    }
}
