# SGAPE v2

**Sistema de Gestión y Administración de Emprendimientos.** Aplicación de escritorio en **WPF con Material Design** y un **bot de Telegram con Groq**. Las dos usan la misma lógica y la misma base de datos **Oracle Autonomous Database**.

Universidad Popular del Cesar · 2026

## Qué cambió frente a la versión 1

| Antes | Ahora |
|---|---|
| Windows Forms | **WPF + MaterialDesignInXamlToolkit**, con MVVM (CommunityToolkit.Mvvm) |
| Un formulario distinto por módulo | **Una sola ventana de crear y editar** (`FormularioDialogo`) y una sola barra de Nuevo, Editar, Eliminar y Buscar (`BarraCrud`) para todos los módulos |
| Sector, cargo y ciudad escritos a mano | **Catálogos**: sectores, cargos sugeridos por sector con salario, los **1.121 municipios DIVIPOLA** y tipos de documento, con **texto predictivo** |
| Documento "CC: 123" en un solo texto | **Tipo y número de documento en campos separados**, validados según el tipo (CC, CE, TI, PA, PPT, PEP) |
| Factura en .txt con cantidad total | **Factura en PDF**, sin cantidad total |
| Reporte HTML | **Reporte en PDF** (QuestPDF). El bot también lo envía |
| Paquetes y vistas que solo se usaban desde SQL Developer | **El programa usa todos los objetos de Oracle** (lista abajo) |
| Venta armada en C#, paso a paso | `sp_sg_registrar_venta`: **una sola transacción** en Oracle |
| Bot con n8n y Ollama | **Bot nuevo en C# con Groq**: function calling con herramientas fijas, corre 24/7 en una VM de Oracle Cloud |
| Hojas de vida solo con la app | **Los aspirantes envían la hoja de vida por el bot**; queda en un BLOB de Oracle |

Se **eliminaron** de la solución el asistente de n8n, el de Ollama y los repositorios CSV.

## Estructura

```
SGAPE.slnx
├── Entidades/      Qué ES cada cosa (Persona → Trabajador / Postulacion, Producto, Venta...)
├── Datos/          DAOs de Oracle + Abstract Factory (leen vistas, escriben tablas, llaman paquetes y procedimientos)
├── Logica/         Servicios con las reglas de negocio + PDF (QuestPDF). Los usan la app Y el bot
├── Presentacion/   WPF + Material Design (MVVM)
├── BotTelegram/    Bot de Telegram (Groq), para un servidor Linux
└── sql/            Script completo de la base de datos, datos de demostración y usuario del bot
```

## Objetos de Oracle y dónde los usa el programa

| Objeto | Lo usa |
|---|---|
| `pkg_sg_nomina` (5 funciones + `listar_nomina`) | Nómina (tarjetas y pestaña Liquidación), Inicio, reporte PDF, bot `/nomina` |
| `pkg_sg_reportes` (6 funciones + 3 procedimientos) | Finanzas, Ventas, Reportes, reporte PDF, herramientas del bot |
| `pkg_sg_telegram` | Sección Telegram (generar código) y bot (vincular, emprendimiento activo) |
| `sp_sg_registrar_venta` | Ventas, botón "Registrar venta" |
| `sp_sg_mover_stock` | Inventario, botón Editar (campo "Movimiento de stock": entrada o salida) |
| `sp_sg_ajustar_precio_venta` | Inventario, botón "Ajustar precios" |
| `sp_sg_productos_stock_bajo` | Inicio, reporte PDF, bot `/stock` y aviso automático de stock bajo |
| `sp_sg_presupuestos_estado` | Finanzas (pestaña Presupuestos), Inicio, bot `/presupuestos` y alertas automáticas |
| `sp_sg_telegram_alertas` + `vw_sg_telegram_alertas` | Bot: `/alertas` y los avisos automáticos (stock bajo, presupuestos, hojas de vida, cierre del día) |
| `sp_sg_resumen_emprendimiento` | Inicio, bot `/resumen` |
| `sp_sg_contratar_postulante` | Vacantes, botón "Contratar" |
| `sp_sg_limpiar_bitacora`, `sp_sg_cerrar_vacantes_vencidas` | Al abrir la app y cada 6 horas en el bot |
| Las 14 vistas `vw_sg_*` | Todas las listas (la app lee de vistas y escribe en tablas) |
| Las 7 funciones `fn_sg_*` | Dentro de las vistas, los paquetes y los disparadores |
| Los 9 disparadores `trg_sg_*` | Se activan solos con cada INSERT o UPDATE de la app o del bot |

## Cambios del 17/11/2026

- **Finanzas y Presupuestos en una sola sección.** Finanzas tiene dos pestañas: *Movimientos* (lo que entró y salió) y *Presupuestos* (el límite mensual por categoría de gasto). Cada gasto registrado mueve su presupuesto.
- **Sin botones Entrada y Salida.** El movimiento de stock está dentro de **Editar** del producto: eliges *Entrada* o *Salida* y la cantidad.
- **El trabajador ya no pide correo.**
- **Errores de validación** (cédula mal escrita, etc.): solo se muestra el mensaje. Antes Visual Studio se detenía en el código porque la excepción salía de un hilo de fondo; ahora la atrapa `Fondo.Run`.
- **Aviso falso "Primero crea un emprendimiento"** al recargar la lista: corregido.
- **Bot con avisos automáticos**: stock bajo, presupuestos al 80 % o excedidos, hojas de vida nuevas y cierre del día a las 7 p. m. Comandos nuevos: `/ventas`, `/postulaciones` (descarga la hoja de vida) y `/alertas`.
  Si tu base ya estaba creada, ejecuta **`sql/sgape_v2_alertas_bot.sql`** una vez.

## Validaciones en tres capas

1. **Interfaz**: campos obligatorios, solo números en montos y cantidades, listas cerradas.
2. **Lógica** (`Validar`): formato de documento según su tipo, correo, celular, nombres con solo letras, fechas no futuras, montos mayores que cero, duplicados, que cada registro pertenezca al emprendimiento.
3. **Oracle**: `CHECK`, `UNIQUE`, llaves foráneas y disparadores (documento, categoría coherente con el tipo, cargo del mismo emprendimiento, vacante abierta).

## Cómo ponerlo en marcha

1. **Base de datos.** En SQL Developer, conectado a tu Autonomous Database:
   - Si tienes la versión 1, ejecuta `sql/00_borrar_todo.sql`. **Esto borra todos los datos.**
   - Ejecuta `sql/sgape_v2_completo.sql` con **F5**. La parte 8 debe mostrar todo en VALID y las pruebas en OK.
   - Opcional: `sql/sgape_v2_datos_demo.sql` crea el usuario `demo@sgape.co` con la contraseña `Demo2026`.
   - Para el bot: `sql/sgape_v2_usuario_bot.sql`. Antes de ejecutarlo, cambia la clave que trae.
2. **Aplicación.** Copia `sql/conexion.ejemplo.json` a `%LOCALAPPDATA%\SGAPE\conexion.json` y llénalo. Abre `SGAPE.slnx` en Visual Studio 2022 (17.14 o superior, con .NET 10). Pon **Presentacion** como proyecto de inicio y pulsa **F5**.
3. **Bot.** Sigue `BotTelegram/DESPLIEGUE.md`.

## Seguridad

- `conexion.json`, el wallet, el token de Telegram y la clave de Groq **nunca** van al repositorio (`.gitignore`).
- El bot usa el usuario `SGAPE_BOT` con permisos mínimos, no `ADMIN`.
- El modelo de IA **no escribe SQL**. Solo puede pedir 12 herramientas fijas, y siempre sobre el emprendimiento del chat vinculado. Registrar un ingreso o un gasto pide confirmación con un botón.
- Las hojas de vida exigen la **autorización de tratamiento de datos (Ley 1581 de 2012)**. Además, `CHECK (autoriza_datos = 'S')` en Oracle.
