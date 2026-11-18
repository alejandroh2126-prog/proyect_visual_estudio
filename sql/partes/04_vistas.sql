/* =====================================================================
   PARTE 4. VISTAS (consultas guardadas). La aplicacion y el bot LEEN las
   listas desde aqui: los calculos (nomina, margen, estado del presupuesto,
   total de la venta, balance) los hace Oracle, no C#.
   ===================================================================== */

-- 4.1 Municipios con su departamento (autocompletado de ciudades)
CREATE OR REPLACE VIEW vw_sg_ciudades AS
SELECT c.codigo,
       c.nombre,
       d.codigo AS departamento_codigo,
       d.nombre AS departamento,
       c.nombre || ' (' || d.nombre || ')' AS nombre_completo
  FROM sg_ciudad c
  JOIN sg_departamento d ON d.codigo = c.departamento_codigo;

-- 4.2 Emprendimientos con el nombre de su sector y de su ciudad
CREATE OR REPLACE VIEW vw_sg_emprendimientos AS
SELECT e.id,
       e.usuario_id,
       e.nombre,
       e.descripcion,
       e.sector_id,
       s.nombre            AS sector,
       e.ciudad_codigo,
       c.nombre_completo   AS ciudad,
       e.fecha_inicio,
       e.estado
  FROM sg_emprendimiento e
  JOIN sg_sector s       ON s.id = e.sector_id
  JOIN vw_sg_ciudades c  ON c.codigo = e.ciudad_codigo;

-- 4.3 Cargos con el numero de trabajadores activos y sus valores de nomina
CREATE OR REPLACE VIEW vw_sg_cargos AS
SELECT c.id,
       c.emprendimiento_id,
       c.plantilla_id,
       c.nombre,
       c.salario_base,
       c.descripcion,
       ROUND(c.salario_base / 2, 2)        AS salario_quincenal,
       fn_sg_deducciones(c.salario_base)   AS deducciones,
       fn_sg_salario_neto(c.salario_base)  AS salario_neto,
       (SELECT COUNT(*) FROM sg_trabajador t
         WHERE t.cargo_id = c.id AND t.estado = 'activo') AS trabajadores_activos
  FROM sg_cargo c;

-- 4.4 Trabajadores: tipo y numero de documento en columnas SEPARADAS, cargo y ciudad
CREATE OR REPLACE VIEW vw_sg_trabajadores AS
SELECT t.id,
       t.emprendimiento_id,
       t.cargo_id,
       c.nombre            AS cargo,
       c.salario_base,
       t.tipo_documento,
       td.nombre           AS tipo_documento_nombre,
       t.numero_documento,
       t.nombre,
       t.apellido,
       t.telefono,
       t.email,
       t.ciudad_codigo,
       ci.nombre_completo  AS ciudad,
       t.fecha_ingreso,
       t.estado
  FROM sg_trabajador t
  JOIN sg_cargo c               ON c.id = t.cargo_id
  JOIN sg_tipo_documento td     ON td.codigo = t.tipo_documento
  LEFT JOIN vw_sg_ciudades ci   ON ci.codigo = t.ciudad_codigo;

-- 4.5 Nomina de los trabajadores activos: quincena, deducciones y neto (usa fn_sg_deducciones y fn_sg_salario_neto)
CREATE OR REPLACE VIEW vw_sg_nomina AS
SELECT t.emprendimiento_id,
       t.id                                 AS trabajador_id,
       t.tipo_documento,
       t.numero_documento,
       t.nombre || ' ' || t.apellido        AS trabajador,
       c.nombre                             AS cargo,
       c.salario_base,
       ROUND(c.salario_base / 2, 2)         AS pago_quincenal,
       ROUND(c.salario_base * 0.04, 2)      AS salud,
       ROUND(c.salario_base * 0.04, 2)      AS pension,
       fn_sg_deducciones(c.salario_base)    AS deducciones,
       fn_sg_salario_neto(c.salario_base)   AS neto
  FROM sg_trabajador t
  JOIN sg_cargo c ON c.id = t.cargo_id
 WHERE t.estado = 'activo';

-- 4.6 Inventario con el margen de cada producto (usa fn_sg_porcentaje_margen)
CREATE OR REPLACE VIEW vw_sg_margen_producto AS
SELECT p.id                                                     AS producto_id,
       p.emprendimiento_id,
       p.nombre,
       p.categoria,
       p.precio_compra,
       p.precio_venta,
       p.stock,
       p.stock_minimo,
       CASE WHEN p.stock <= p.stock_minimo THEN 'S' ELSE 'N' END AS stock_bajo,
       p.precio_venta - p.precio_compra                          AS margen_valor,
       fn_sg_porcentaje_margen(p.precio_compra, p.precio_venta)  AS margen_porcentaje,
       p.stock * p.precio_compra                                 AS valor_inventario
  FROM sg_producto p;

-- 4.7 Productos con stock bajo (stock actual menor o igual al minimo)
CREATE OR REPLACE VIEW vw_sg_stock_bajo AS
SELECT p.id                       AS producto_id,
       p.emprendimiento_id,
       p.nombre,
       p.categoria,
       p.stock,
       p.stock_minimo,
       p.stock_minimo - p.stock   AS faltante
  FROM sg_producto p
 WHERE p.stock <= p.stock_minimo;

-- 4.8 Movimientos con el nombre de la categoria
CREATE OR REPLACE VIEW vw_sg_movimientos AS
SELECT m.id,
       m.emprendimiento_id,
       m.fecha,
       m.tipo,
       m.categoria_id,
       cm.nombre AS categoria,
       m.descripcion,
       m.monto,
       m.referencia,
       CASE WHEN m.tipo IN ('Venta', 'Ingreso') THEN 'S' ELSE 'N' END AS es_ingreso,
       CASE WHEN m.tipo IN ('Compra', 'Gasto')  THEN 'S' ELSE 'N' END AS es_gasto
  FROM sg_movimiento m
  JOIN sg_categoria_movimiento cm ON cm.id = m.categoria_id;

-- 4.9 Uso de cada presupuesto: gastado, disponible, porcentaje y estado (OK, ALERTA desde 80 %, EXCEDIDO)
CREATE OR REPLACE VIEW vw_sg_uso_presupuesto AS
SELECT x.presupuesto_id,
       x.emprendimiento_id,
       x.categoria_id,
       x.categoria,
       x.mes,
       x.monto_limite,
       x.gastado,
       x.monto_limite - x.gastado AS disponible,
       ROUND(x.gastado / x.monto_limite * 100, 2) AS porcentaje_uso,
       CASE WHEN x.gastado > x.monto_limite                 THEN 'EXCEDIDO'
            WHEN x.gastado / x.monto_limite * 100 >= 80     THEN 'ALERTA'
            ELSE 'OK'
       END AS estado
  FROM (SELECT p.id AS presupuesto_id,
               p.emprendimiento_id,
               p.categoria_id,
               cm.nombre AS categoria,
               p.mes,
               p.monto_limite,
               NVL((SELECT SUM(m.monto)
                      FROM sg_movimiento m
                     WHERE m.emprendimiento_id = p.emprendimiento_id
                       AND m.tipo IN ('Compra', 'Gasto')
                       AND m.categoria_id = p.categoria_id
                       AND TO_CHAR(m.fecha, 'YYYY-MM') = p.mes), 0) AS gastado
          FROM sg_presupuesto p
          JOIN sg_categoria_movimiento cm ON cm.id = p.categoria_id) x;

-- 4.10 Historial de ventas (usa fn_sg_total_venta)
CREATE OR REPLACE VIEW vw_sg_ventas_resumen AS
SELECT v.id                         AS venta_id,
       v.emprendimiento_id,
       v.fecha,
       v.observacion,
       (SELECT COUNT(*) FROM sg_detalle_venta d WHERE d.venta_id = v.id) AS productos,
       fn_sg_total_venta(v.id)      AS total
  FROM sg_venta v;

-- 4.11 Resumen de cada emprendimiento (pantalla de Inicio y comando /resumen del bot; usa fn_sg_balance)
CREATE OR REPLACE VIEW vw_sg_resumen_emprendimiento AS
SELECT e.id AS emprendimiento_id,
       e.usuario_id,
       e.nombre,
       s.nombre AS sector,
       (SELECT COUNT(*) FROM sg_trabajador t
         WHERE t.emprendimiento_id = e.id AND t.estado = 'activo')        AS trabajadores_activos,
       (SELECT COUNT(*) FROM sg_producto p
         WHERE p.emprendimiento_id = e.id)                                AS productos,
       (SELECT COUNT(*) FROM sg_producto p
         WHERE p.emprendimiento_id = e.id AND p.stock <= p.stock_minimo)  AS productos_stock_bajo,
       (SELECT COUNT(*) FROM sg_venta v
         WHERE v.emprendimiento_id = e.id
           AND TRUNC(v.fecha, 'MM') = TRUNC(SYSDATE, 'MM'))               AS ventas_del_mes,
       (SELECT COUNT(*) FROM sg_postulacion po JOIN sg_vacante va ON va.id = po.vacante_id
         WHERE va.emprendimiento_id = e.id AND po.estado = 'recibida')    AS postulaciones_nuevas,
       fn_sg_balance(e.id)                                                AS balance
  FROM sg_emprendimiento e
  JOIN sg_sector s ON s.id = e.sector_id;

-- 4.12 Vacantes con su cargo, ciudad y numero de postulaciones
CREATE OR REPLACE VIEW vw_sg_vacantes AS
SELECT v.id,
       v.emprendimiento_id,
       e.nombre            AS emprendimiento,
       v.cargo_id,
       c.nombre            AS cargo,
       c.salario_base,
       v.titulo,
       v.descripcion,
       v.ciudad_codigo,
       ci.nombre_completo  AS ciudad,
       v.fecha_publicacion,
       v.fecha_cierre,
       v.estado,
       (SELECT COUNT(*) FROM sg_postulacion p WHERE p.vacante_id = v.id) AS postulaciones
  FROM sg_vacante v
  JOIN sg_emprendimiento e   ON e.id = v.emprendimiento_id
  JOIN sg_cargo c            ON c.id = v.cargo_id
  JOIN vw_sg_ciudades ci     ON ci.codigo = v.ciudad_codigo;

-- 4.13 Postulaciones SIN el archivo (el BLOB se descarga aparte, solo cuando se pide)
CREATE OR REPLACE VIEW vw_sg_postulaciones AS
SELECT p.id,
       p.vacante_id,
       v.emprendimiento_id,
       v.titulo                 AS vacante,
       p.tipo_documento,
       p.numero_documento,
       p.nombre,
       p.apellido,
       p.telefono,
       p.email,
       p.ciudad_codigo,
       ci.nombre_completo       AS ciudad,
       p.cv_nombre,
       p.cv_tipo,
       DBMS_LOB.GETLENGTH(p.cv) AS cv_bytes,
       p.fecha,
       p.estado
  FROM sg_postulacion p
  JOIN sg_vacante v        ON v.id = p.vacante_id
  JOIN vw_sg_ciudades ci   ON ci.codigo = p.ciudad_codigo;
