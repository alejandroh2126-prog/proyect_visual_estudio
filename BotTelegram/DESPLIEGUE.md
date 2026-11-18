# Bot de SGAPE en Oracle Cloud (24/7)

El bot debe correr en un servidor y no en tu PC, porque el dueño del emprendimiento no tendrá el computador encendido todo el tiempo. Esta guía usa una **VM Always Free de Oracle Cloud**, la misma nube donde ya está tu base de datos.

## 1. Lo que necesitas

| Qué | Dónde se consigue |
|---|---|
| Token del bot | En Telegram, habla con **@BotFather**: `/newbot` → nombre → usuario terminado en `bot`. Si el token anterior quedó expuesto, revócalo con `/revoke`. |
| Clave de Groq | https://console.groq.com/keys → *Create API Key* |
| Wallet de la base de datos | Oracle Cloud → Autonomous Database → **Database connection** → *Download wallet* |
| Usuario `SGAPE_BOT` | Ejecuta `sql/sgape_v2_usuario_bot.sql` (cambia la clave antes) |
| Alertas automáticas | Si tu base se creó antes del 17/11/2026, ejecuta una vez `sql/sgape_v2_alertas_bot.sql` |

No pegues el token ni la clave en ningún chat, documento o repositorio. Van solo en el archivo `/etc/sgape-bot.env` de la VM.

## 2. Crear la VM

1. Oracle Cloud → **Compute → Instances → Create instance**.
2. Imagen: **Ubuntu 24.04**. Forma Always Free: `VM.Standard.E2.1.Micro` (x64) o `VM.Standard.A1.Flex` (ARM).
3. Descarga la llave SSH que te ofrece y crea la instancia.
4. El bot no necesita puertos abiertos, porque usa *long polling* (él llama a Telegram). Deja solo SSH (22).

## 3. Compilar el bot (en tu PC)

```bat
cd BotTelegram\despliegue
publicar-bot.bat
```

Queda `PublicadoBot\sgape-bot`, que trae .NET adentro y no requiere instalar nada en la VM. Si tu VM es ARM, cambia `linux-x64` por `linux-arm64` en el `.bat`.

## 4. Subir y configurar (desde tu PC)

```bash
scp -i llave.key PublicadoBot/sgape-bot ubuntu@IP_DE_LA_VM:/tmp/
scp -i llave.key -r Wallet_SGAPE ubuntu@IP_DE_LA_VM:/tmp/wallet
scp -i llave.key BotTelegram/despliegue/sgape-bot.service BotTelegram/despliegue/sgape-bot.env.ejemplo ubuntu@IP_DE_LA_VM:/tmp/
```

En la VM (`ssh -i llave.key ubuntu@IP_DE_LA_VM`):

```bash
sudo apt update && sudo apt install -y fonts-dejavu-core libfontconfig1     # fuentes para los PDF
sudo useradd --system --home /opt/sgape-bot --shell /usr/sbin/nologin sgape
sudo mkdir -p /opt/sgape-bot
sudo mv /tmp/sgape-bot /opt/sgape-bot/ && sudo chmod +x /opt/sgape-bot/sgape-bot
sudo mv /tmp/wallet /opt/sgape-bot/wallet
sudo chown -R sgape:sgape /opt/sgape-bot && sudo chmod 700 /opt/sgape-bot/wallet

sudo mv /tmp/sgape-bot.env.ejemplo /etc/sgape-bot.env
sudo nano /etc/sgape-bot.env          # llena TELEGRAM_BOT_TOKEN y SGAPE_DB_CLAVE (la clave de Groq la toma de Oracle)
sudo chmod 600 /etc/sgape-bot.env

sudo mv /tmp/sgape-bot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now sgape-bot
```

## 5. Comprobar

```bash
sudo systemctl status sgape-bot        # debe decir active (running)
sudo journalctl -u sgape-bot -f        # registro en vivo
```

En Telegram, escribe `/start` a tu bot. En la aplicación, ve a **Telegram → Generar código** y envía `/vincular 123456`.

## 6. Qué hace el bot sin que nadie escriba

Cada 15 minutos (`SGAPE_ALERTAS_MINUTOS`) revisa Oracle y avisa a los chats vinculados:

- ⚠️ productos que acaban de quedar por debajo del mínimo,
- 🟠/🔴 presupuestos que llegan al 80 % o se pasan,
- 📄 hojas de vida nuevas (con botón para descargarlas),
- 🌙 el cierre del día a las 19 h (`SGAPE_RESUMEN_HORA`, `-1` lo apaga).

Solo avisa lo nuevo; el dueño lo apaga o enciende con `/alertas`.

## 7. Probarlo en tu PC antes de subirlo

En Visual Studio: clic derecho en **BotTelegram** → *Propiedades* → *Depurar* → *Abrir la interfaz de usuario de perfiles de inicio* → *Variables de entorno*, y agrega `TELEGRAM_BOT_TOKEN`, `SGAPE_DB_USUARIO`, `SGAPE_DB_CLAVE`, `SGAPE_DB_DATASOURCE` y `SGAPE_DB_WALLET` (ruta a la carpeta del wallet). Luego clic derecho en BotTelegram → *Depurar* → *Iniciar nueva instancia*. Mientras corre, responde en Telegram; al cerrarlo, deja de responder. Por eso, para que funcione con el PC apagado, va en la VM.

No pongas esas variables en un archivo que se suba a Git (`launchSettings.json` está en `.gitignore`).

## 8. Actualizar el bot

Vuelve a compilar, sube el archivo y ejecuta:

```bash
sudo systemctl stop sgape-bot
sudo mv /tmp/sgape-bot /opt/sgape-bot/sgape-bot && sudo chown sgape:sgape /opt/sgape-bot/sgape-bot && sudo chmod +x /opt/sgape-bot/sgape-bot
sudo systemctl start sgape-bot
```

## Problemas frecuentes

| Síntoma | Causa probable |
|---|---|
| `No se pudo conectar a Oracle` | Wallet mal copiado, `SGAPE_DB_DATASOURCE` distinto al alias de `tnsnames.ora`, o la base de datos está detenida (Always Free se detiene tras días sin uso: *Start* en Oracle Cloud) |
| `La base de datos no tiene los objetos de SGAPE v2` | Falta ejecutar `sgape_v2_completo.sql` o `sgape_v2_usuario_bot.sql` |
| El bot no responde | `journalctl -u sgape-bot -f`. Revisa el token. Solo puede haber **una** copia del bot en marcha con el mismo token |
| "El asistente está muy ocupado" | Se alcanzó el límite gratuito de Groq; los comandos (`/resumen`, `/stock`...) siguen funcionando porque no usan IA |
| El PDF sale sin texto | Faltan fuentes: `sudo apt install fonts-dejavu-core` |

## Clave de Groq para todos (desde el 17/11/2026)

1. Ejecuta `sql/sgape_v2_configuracion.sql` como ADMIN (crea `sg_configuracion` y da lectura a SGAPE_BOT).
2. En la aplicación, sección **Asistente**, pega la clave y pulsa **Guardar para todos**. Se prueba con Groq y se guarda en Oracle.
3. Desde ahí, cualquier computador que abra SGAPE y el bot usan esa clave sin pedirla. `GROQ_API_KEY` en variables de entorno sigue funcionando y tiene prioridad.
