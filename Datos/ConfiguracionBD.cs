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
    /// 2) Si no existen, el archivo %LOCALAPPDATA%\SGAPE\conexion.json (para desarrollo en tu PC).
    /// 3) Si tampoco existe, conexion\conexion.json junto a SGAPE.exe (lo pone el instalador: sirve en cualquier computador).
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

        /// <summary>Conexion que trae el instalador: carpeta "conexion" junto a SGAPE.exe (usuario SGAPE_APP + wallet).</summary>
        public static string RutaInstalada
        {
            get { return Path.Combine(AppContext.BaseDirectory, "conexion", "conexion.json"); }
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

            // 2) El archivo del usuario (%LOCALAPPDATA%) y 3) el que trae el instalador junto al programa
            string? ruta = File.Exists(RutaArchivo) ? RutaArchivo
                         : File.Exists(RutaInstalada) ? RutaInstalada
                         : null;
            if (ruta == null)
                throw new ReglaDeNegocioException(
                    "No se encontró la conexión a Oracle. Reinstala SGAPE con el instalador oficial, o crea el archivo:" +
                    Environment.NewLine + RutaArchivo + Environment.NewLine + Environment.NewLine +
                    "copiando sql/conexion.ejemplo.json y escribiendo los datos de tu base de datos.");

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ruta)))
                {
                    JsonElement raiz = doc.RootElement;
                    string wallet = Leer(raiz, "carpetaWallet").Trim().Trim('"');
                    // Una ruta relativa ("wallet") se toma desde la carpeta del archivo de conexion
                    if (wallet.Length > 0 && !Path.IsPathRooted(wallet))
                        wallet = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ruta)!, wallet));
                    return new ConfiguracionBD
                    {
                        Usuario = Leer(raiz, "usuario").Trim(),
                        Clave = Leer(raiz, "clave").Trim(),
                        DataSource = Leer(raiz, "dataSource").Trim(),
                        CarpetaWallet = wallet
                    };
                }
            }
            catch (JsonException)
            {
                throw new ReglaDeNegocioException("El archivo de conexión no tiene un formato válido: " + ruta);
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
