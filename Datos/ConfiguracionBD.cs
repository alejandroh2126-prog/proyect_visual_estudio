using System;
using System.IO;
using System.Text.Json;
using Entidades;

namespace Datos
{
    /// <summary>
    /// UNA SOLA MISION: saber como conectarse a Oracle.
    /// 1) Variables de entorno (las usa el bot en el servidor):
    ///    SGAPE_DB_USUARIO, SGAPE_DB_CLAVE, SGAPE_DB_DATASOURCE, SGAPE_DB_WALLET
    /// 2) Si no existen, el archivo %LOCALAPPDATA%\SGAPE\conexion.json (lo usa la aplicacion de escritorio).
    /// La clave NUNCA va en el codigo ni en el repositorio de Git.
    /// </summary>
    public class ConfiguracionBD
    {
        public string Usuario { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;

        /// <summary>Alias del tnsnames.ora (ej: sgape_low) o Easy Connect (ej: localhost:1521/XEPDB1).</summary>
        public string DataSource { get; set; } = string.Empty;

        /// <summary>Carpeta del wallet descomprimido (Autonomous Database). Vacio si es Oracle local.</summary>
        public string CarpetaWallet { get; set; } = string.Empty;

        public static string RutaArchivo
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "SGAPE", "conexion.json");
            }
        }

        public static ConfiguracionBD Cargar()
        {
            string? usuarioEnv = Environment.GetEnvironmentVariable("SGAPE_DB_USUARIO");
            if (!string.IsNullOrWhiteSpace(usuarioEnv))
            {
                // Trim: un espacio pegado por error (" SGAPE_BOT") hacia que Oracle respondiera ORA-01017
                return new ConfiguracionBD
                {
                    Usuario = usuarioEnv.Trim(),
                    Clave = (Environment.GetEnvironmentVariable("SGAPE_DB_CLAVE") ?? string.Empty).Trim(),
                    DataSource = (Environment.GetEnvironmentVariable("SGAPE_DB_DATASOURCE") ?? string.Empty).Trim(),
                    CarpetaWallet = (Environment.GetEnvironmentVariable("SGAPE_DB_WALLET") ?? string.Empty).Trim().Trim('"')
                };
            }

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
                    return new ConfiguracionBD
                    {
                        Usuario = Leer(raiz, "usuario").Trim(),
                        Clave = Leer(raiz, "clave").Trim(),
                        DataSource = Leer(raiz, "dataSource").Trim(),
                        CarpetaWallet = Leer(raiz, "carpetaWallet").Trim().Trim('"')
                    };
                }
            }
            catch (JsonException)
            {
                throw new ReglaDeNegocioException("El archivo conexion.json no tiene un formato válido.");
            }
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
