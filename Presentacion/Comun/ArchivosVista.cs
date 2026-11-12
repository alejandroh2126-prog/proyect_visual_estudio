using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Presentacion.Comun
{
    internal static class ArchivosVista
    {
        public static string? Elegir(bool imagen, bool pdf)
        {
            using OpenFileDialog d = new OpenFileDialog();
            d.Filter = imagen && pdf ? "Documentos|*.jpg;*.jpeg;*.png;*.pdf" : imagen ? "Imágenes|*.jpg;*.jpeg;*.png" : "PDF|*.pdf";
            d.CheckFileExists = true;
            if (d.ShowDialog() != DialogResult.OK) return null;
            if (new FileInfo(d.FileName).Length > 10L * 1024 * 1024) throw new Logica.Utilidades.ReglaDeNegocioException("El archivo supera 10 MB.");
            if (imagen && !Path.GetExtension(d.FileName).Equals(".pdf", System.StringComparison.OrdinalIgnoreCase))
            {
                using FileStream s = File.OpenRead(d.FileName);
                using Image i = Image.FromStream(s, true, true);
                if ((long)i.Width * i.Height > 25000000) throw new Logica.Utilidades.ReglaDeNegocioException("La imagen supera 25 megapíxeles.");
            }
            return d.FileName;
        }
        public static void MostrarFoto(PictureBox caja, string ruta)
        {
            using FileStream s = File.OpenRead(ruta);
            using Image original = Image.FromStream(s, true, true);
            Image copia = new Bitmap(original);
            Image? anterior = caja.Image; caja.Image = copia; anterior?.Dispose();
        }
    }
}
