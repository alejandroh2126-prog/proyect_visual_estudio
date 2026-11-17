# SGAPE

**Sistema de Gestión y Administración de Emprendimientos** — aplicación de escritorio para Windows hecha en **C# / .NET (Windows Forms)** con **arquitectura en capas**.

Universidad Popular del Cesar · Ingeniería de Sistemas · 2026

## ¿Qué puede hacer?

| Módulo | Funciones |
|---|---|
| Cuenta | Registro e inicio de sesión (contraseñas con PBKDF2 + sal) |
| Emprendimientos | Crear, editar y eliminar (con todos sus datos) |
| Nómina | Cargos, trabajadores, deducciones (salud 4 % + pensión 4 %), nómina quincenal/mensual/neta |
| Inventario | Productos, entradas y salidas de stock, alertas de stock bajo |
| Ventas | Carrito, descuento automático de stock, historial y factura en texto |
| Finanzas | Ingresos y gastos por categoría, balance e historial de movimientos |
| Presupuestos | Límite mensual por categoría con alerta desde el 80 % |
| Reportes | Estadísticas y reporte HTML con gráficas |
| Asistente | Preguntas frecuentes sobre la plataforma y la administración de un negocio |

## Arquitectura

```
SGAPE.slnx
├── Entidades/      → Qué ES cada cosa (Persona, Trabajador, Producto, Venta...). Sin dependencias.
├── Logica/         → Qué HACE el sistema (servicios, reglas de negocio, repositorios CSV).
│   ├── Repositorios/   guardar y leer datos (RepositorioCsv<T> + uno por entidad)
│   ├── Servicios/      un servicio por tarea (Autenticación, Nómina, Inventario, Ventas...)
│   └── Utilidades/     validaciones, formatos, CSV seguro, rutas, sesión
└── Presentacion/   → Lo que VE el usuario (Windows Forms, sin Designer).
    ├── Comun/          tema, tarjetas, mensajes, VistaBase, DialogoBase
    ├── Vistas/         una vista por módulo
    └── Dialogos/       formularios emergentes
```

Referencias entre proyectos: `Presentacion → Logica → Entidades` (y `Presentacion → Entidades`).
La capa de presentación **nunca** toca archivos: solo habla con `FabricaServicios`.

### Pilares y principios aplicados

- **Abstracción**: `Persona` (clase abstracta), `RepositorioCsv<T>`, `VistaBase`, `DialogoBase`.
- **Encapsulamiento**: propiedades con validación (`Persona`), `Producto.Stock` solo cambia con `AgregarStock/ReducirStock`.
- **Herencia**: `Trabajador : Persona`, repositorios que heredan de `RepositorioCsv<T>`, vistas que heredan de `VistaBase`.
- **Polimorfismo**: `Persona.TipoPersona`, `Serializar/Deserializar` de cada repositorio, `Recargar()` de cada vista.
- **SRP (una clase, una misión)**: cada servicio y cada vista tiene una única razón para cambiar.
- **Bajo acoplamiento / alta cohesión**: dependencias entregadas por constructor y ensambladas solo en `FabricaServicios`.

## Datos

Se guardan en archivos CSV en `%LOCALAPPDATA%\SGAPE\datos`. Las facturas y reportes se crean en `Documentos\SGAPE`.
Desinstalar la aplicación **no** borra tus datos.

## Compilar y ejecutar

1. Visual Studio Community con la carga de trabajo **Desarrollo de escritorio con .NET**.
2. Abrir `SGAPE.slnx`, establecer **Presentacion** como proyecto de inicio y pulsar **F5**.

## Generar el instalador

1. Doble clic en `publicar.bat` → crea `Publicado\SGAPE.exe` (incluye .NET; no hay que instalarlo aparte).
2. Abrir `Instalador\SGAPE.iss` con [Inno Setup](https://jrsoftware.org/isinfo.php) y pulsar **Compile**.
3. El instalador queda en `Instalador\Salida\SGAPE-Instalador.exe` (nombre fijo, versión 1.1.0). Súbelo como archivo de un Release de GitHub para que funcione el botón de descarga del sitio web.

## Pruebas automáticas

`dotnet run --project Pruebas` ejecuta 41 comprobaciones de la lógica (cuentas, archivos, privacidad, empleo, historial y Nómina) en una carpeta temporal, sin tocar tus datos.

## Perfiles y empleo (prototipo local)

- **Mi perfil:** foto y colores por cuenta; los cambios se aplican al instante al guardar.
- **Nómina:** tipos de documento, ficha ampliada y archivos privados de empleados.
- **Empleo:** cuentas de trabajador, perfiles profesionales, hojas de vida PDF, vacantes y postulaciones. El trabajador sube su foto de perfil (fondo blanco). Los emprendedores buscan candidatos, abren su perfil (foto, datos profesionales, experiencia declarada y hoja de vida) y solo ven lo que el trabajador compartió.
- **Historial laboral:** ofertas aceptadas, vinculaciones, finalizaciones, motivos privados (renuncia, despido con o sin justa causa, abandono, fin de contrato, entre otros), solicitudes de corrección y experiencias declaradas.

Los datos y adjuntos se guardan localmente en `%LOCALAPPDATA%\SGAPE\datos`. Las cuentas antiguas se leen como emprendedoras y los documentos anteriores como CC. Para probar, usa dos cuentas de SGAPE en la **misma cuenta de Windows** y cierra sesión para alternarlas. No existe aún un servidor compartido ni sincronización entre computadores. No uses documentos reales en una demostración; el almacenamiento local no protege contra usuarios con acceso directo al sistema de archivos. No es facturación, nómina ni contratación legal certificada. Tras aceptar la oferta, el empleador usa **Agregar a Nómina**: el cargo y el salario vienen de la vacante y el número de documento, que es privado, lo escribe el empleador. No se incluyen fotos ni documentos de ejemplo reales en Git.

## Autores

Alejandro Henríquez Quinchia y colaboradores · Universidad Popular del Cesar