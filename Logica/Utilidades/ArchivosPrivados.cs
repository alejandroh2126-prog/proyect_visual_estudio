using System;
using System.IO;

namespace Logica.Utilidades
{
    // Archivos fuera del repositorio. Los nombres originales nunca se usan como rutas.
    public static class ArchivosPrivados
    {
        private const int MaxBytes = 10 * 1024 * 1024;
        private static string Carpeta(string ambito)
        {
            if (string.IsNullOrWhiteSpace(ambito) || ambito.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || ambito.Contains(".."))
                throw new ReglaDeNegocioException("Ámbito de archivo inválido.");
            string ruta = Path.Combine(RutasApp.CarpetaDatos, "adjuntos", ambito);
            Directory.CreateDirectory(ruta);
            return ruta;
        }
        public static string Importar(string ambito, string origen, bool imagen, bool pdf)
        {
            string ext = Path.GetExtension(origen).ToLowerInvariant();
            if (!(imagen && (ext == ".jpg" || ext == ".jpeg" || ext == ".png")) && !(pdf && ext == ".pdf"))
                throw new ReglaDeNegocioException("Formato no permitido. Usa JPG, PNG o PDF según el campo.");
            using FileStream entrada = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (entrada.Length < 8 || entrada.Length > MaxBytes)
                throw new ReglaDeNegocioException("El archivo debe tener entre 8 bytes y 10 MB.");
            byte[] firma = new byte[8];
            entrada.ReadExactly(firma);
            bool valida = ext == ".pdf" ? firma[0] == 37 && firma[1] == 80 && firma[2] == 68 && firma[3] == 70 && firma[4] == 45
                : ext == ".png" ? firma[0] == 137 && firma[1] == 80 && firma[2] == 78 && firma[3] == 71 && firma[4] == 13 && firma[5] == 10 && firma[6] == 26 && firma[7] == 10
                : firma[0] == 255 && firma[1] == 216 && firma[2] == 255;
            if (!valida) throw new ReglaDeNegocioException("El contenido no coincide con el formato del archivo.");
            entrada.Position = 0;
            string nombre = Guid.NewGuid().ToString("N") + ext;
            string destino = Path.Combine(Carpeta(ambito), nombre);
            try
            {
                using FileStream salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                entrada.CopyTo(salida);
            }
            catch { if (File.Exists(destino)) File.Delete(destino); throw; }
            return nombre;
        }
        public static string Ruta(string ambito, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre) || Path.GetFileName(nombre) != nombre || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(nombre), "N", out _))
                throw new ReglaDeNegocioException("No hay un archivo válido guardado.");
            string ruta = Path.Combine(Carpeta(ambito), nombre);
            if (!File.Exists(ruta)) throw new ReglaDeNegocioException("El archivo ya no está disponible.");
            return ruta;
        }
        public static void Borrar(string ambito, string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return;
            try { File.Delete(Ruta(ambito, nombre)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ReglaDeNegocioException) { }
        }
    }
}
