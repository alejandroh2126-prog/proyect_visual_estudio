using System;
using System.IO;
using System.Text.Json;
using Logica.Utilidades;

namespace Logica.Datos
{
    /// <summary>
    /// UNA SOLA MISION: leer de donde se guardan los datos y como conectarse.
    /// Lee el archivo %LOCALAPPDATA%\SGAPE\conexion.json. La clave de la base de datos vive
    /// SOLO en ese archivo (en tu computador), nunca dentro del codigo ni del repositorio de Git.
    /// </summary>
    public class ConfiguracionBD
    {
        public string Usuario { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;

        /// <summary>Alias del tnsnames.ora (ej: sgape_low) o cadena Easy Connect (ej: localhost:1521/XEPDB1).</summary>
        public string DataSource { get; set; } = string.Empty;

        /// <summary>Carpeta del wallet descomprimido (solo para Autonomous Database). Vacio si es Oracle local.</summary>
        public string CarpetaWallet { get; set; } = string.Empty;

        public static string RutaArchivo
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SGAPE", "conexion.json");
            }
        }

        public static ConfiguracionBD Cargar()
        {
            ConfiguracionBD config = new ConfiguracionBD();
            if (!File.Exists(RutaArchivo))
                throw new ReglaDeNegocioException(
                    "No se encontró el archivo de conexión a Oracle:" + Environment.NewLine + RutaArchivo +
                    Environment.NewLine + Environment.NewLine +
                    "Créalo copiando sql/conexion.ejemplo.json y escribiendo los datos de tu base de datos.");

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(RutaArchivo)))
                {
                    JsonElement raiz = doc.RootElement;
                    config.Usuario = Leer(raiz, "usuario");
                    config.Clave = Leer(raiz, "clave");
                    config.DataSource = Leer(raiz, "dataSource");
                    config.CarpetaWallet = Leer(raiz, "carpetaWallet");
                }
            }
            catch (JsonException)
            {
                throw new ReglaDeNegocioException("El archivo conexion.json no tiene un formato válido.");
            }
            catch (InvalidOperationException)
            {
                throw new ReglaDeNegocioException("El archivo conexion.json no tiene un formato válido.");
            }
            return config;
        }

        private static string Leer(JsonElement raiz, string propiedad)
        {
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty(propiedad, out JsonElement valor)
                && valor.ValueKind == JsonValueKind.String)
                return valor.GetString() ?? string.Empty;
            return string.Empty;
        }
    }
}
