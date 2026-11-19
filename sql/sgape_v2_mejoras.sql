/* =====================================================================
   SGAPE v2 - MEJORAS DE NOMINA, HORA DE COLOMBIA Y APORTES
   SQL Developer, conectado como ADMIN: abrir y pulsar F5.
   Se puede ejecutar varias veces sin problema (lo ya hecho se salta).

   - Cargos: tipo de horario (tiempo completo, medio tiempo, por turnos),
     salario minimo segun el horario y nombre que no se cambia.
   - Emprendimientos: si el sector es "Otro" se escribe cual.
   - Cargos sugeridos: los mas comunes de cualquier negocio (sector Otro).
   - Trabajadores: el mismo numero de documento no se repite aunque sea
     de otro tipo (CC 123 y CE 123).
   - Hora de Colombia: Oracle Autonomous usa UTC (5 horas adelante); las
     ventas de la noche quedaban con la fecha del dia siguiente y no
     salian en "Hoy". Se corrigen las fechas ya guardadas una sola vez.
   - Aportes: fecha limite para devolverle al socio.
   ===================================================================== */
SET DEFINE OFF;
SET SERVEROUTPUT ON;

DECLARE
  PROCEDURE hacer(p_sql VARCHAR2, p_ok VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_sql;
    DBMS_OUTPUT.PUT_LINE('OK  ' || p_ok);
  EXCEPTION
    WHEN OTHERS THEN
      -- -1430 columna ya existe, -955 ya existe, -2443 restriccion no existe, -2261/-2264 ya existe, -1442 ya es NOT NULL
      IF SQLCODE IN (-1430, -955, -2443, -2261, -2264, -1442, -1451) THEN
        DBMS_OUTPUT.PUT_LINE('--  ' || p_ok || ' (ya estaba)');
      ELSE
        DBMS_OUTPUT.PUT_LINE('ERROR ' || p_ok || ': ' || SQLERRM);
      END IF;
  END;
BEGIN
  hacer('ALTER TABLE sg_cargo ADD (tipo_horario VARCHAR2(20) DEFAULT ''Tiempo completo'' NOT NULL)', 'sg_cargo.tipo_horario');
  hacer('ALTER TABLE sg_cargo ADD CONSTRAINT ck_sg_cargo_horario CHECK (tipo_horario IN (''Tiempo completo'', ''Medio tiempo'', ''Por turnos''))', 'ck_sg_cargo_horario');
  hacer('ALTER TABLE sg_emprendimiento ADD (sector_otro VARCHAR2(80))', 'sg_emprendimiento.sector_otro');
  hacer('ALTER TABLE sg_obligacion_socio ADD (fecha_limite DATE)', 'sg_obligacion_socio.fecha_limite');

  -- Documento: unico por numero (sin importar el tipo). NOVALIDATE: no rechaza los repetidos que ya existan,
  -- pero desde ahora no deja crear otro con el mismo numero.
  hacer('CREATE INDEX ix_sg_trab_num ON sg_trabajador (emprendimiento_id, numero_documento)', 'ix_sg_trab_num');
  hacer('ALTER TABLE sg_trabajador ADD CONSTRAINT uq_sg_trab_num UNIQUE (emprendimiento_id, numero_documento) USING INDEX ix_sg_trab_num ENABLE NOVALIDATE', 'uq_sg_trab_num');
  hacer('ALTER TABLE sg_trabajador DROP CONSTRAINT uq_sg_trab_doc', 'se quita uq_sg_trab_doc (tipo + numero)');

  -- Fechas nuevas con la hora de Colombia
  hacer('ALTER TABLE sg_venta MODIFY (fecha DEFAULT CAST(SYSTIMESTAMP AT TIME ZONE ''America/Bogota'' AS DATE))', 'sg_venta.fecha hora Colombia');
  hacer('ALTER TABLE sg_movimiento MODIFY (fecha DEFAULT CAST(SYSTIMESTAMP AT TIME ZONE ''America/Bogota'' AS DATE))', 'sg_movimiento.fecha hora Colombia');
  hacer('ALTER TABLE sg_vacante MODIFY (fecha_publicacion DEFAULT CAST(SYSTIMESTAMP AT TIME ZONE ''America/Bogota'' AS DATE))', 'sg_vacante.fecha_publicacion hora Colombia');
  hacer('ALTER TABLE sg_postulacion MODIFY (fecha DEFAULT CAST(SYSTIMESTAMP AT TIME ZONE ''America/Bogota'' AS DATE))', 'sg_postulacion.fecha hora Colombia');
END;
/

-- ---------- funciones ----------
CREATE OR REPLACE FUNCTION fn_sg_ahora RETURN DATE IS
BEGIN
    RETURN CAST(SYSTIMESTAMP AT TIME ZONE 'America/Bogota' AS DATE);
END fn_sg_ahora;
/

CREATE OR REPLACE FUNCTION fn_sg_salario_minimo (p_tipo_horario IN VARCHAR2 DEFAULT 'Tiempo completo') RETURN NUMBER IS
    c_smmlv CONSTANT NUMBER := 1750905;
BEGIN
    IF p_tipo_horario IN ('Medio tiempo', 'Por turnos') THEN
        RETURN ROUND(c_smmlv / 2);
    END IF;
    RETURN c_smmlv;
END fn_sg_salario_minimo;
/

-- ---------- corregir UNA vez las fechas guardadas en UTC (ventas y movimientos automaticos) ----------
DECLARE
  v_hecho NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_hecho FROM sg_configuracion WHERE clave = 'MIGRACION_HORA_COLOMBIA';
  IF v_hecho = 0 THEN
    UPDATE sg_venta SET fecha = fecha - 5 / 24;
    DBMS_OUTPUT.PUT_LINE('OK  ' || SQL%ROWCOUNT || ' ventas pasadas a hora de Colombia');
    UPDATE sg_movimiento SET fecha = fecha - 5 / 24 WHERE referencia LIKE 'venta-%' OR referencia LIKE 'producto-%';
    DBMS_OUTPUT.PUT_LINE('OK  ' || SQL%ROWCOUNT || ' movimientos de ventas/inventario pasados a hora de Colombia');
    UPDATE sg_postulacion SET fecha = fecha - 5 / 24;
    INSERT INTO sg_configuracion (clave, valor) VALUES ('MIGRACION_HORA_COLOMBIA', TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI'));
    COMMIT;
  ELSE
    DBMS_OUTPUT.PUT_LINE('--  las fechas ya estaban en hora de Colombia');
  END IF;
END;
/

-- ---------- cargos sugeridos mas comunes (sector Otro) ----------
DECLARE
  v_otro NUMBER;
  PROCEDURE cargo(p_nombre VARCHAR2, p_salario NUMBER) IS
  BEGIN
    MERGE INTO sg_cargo_plantilla p
    USING (SELECT v_otro AS sector_id, p_nombre AS nombre FROM dual) x
       ON (p.sector_id = x.sector_id AND p.nombre = x.nombre)
     WHEN NOT MATCHED THEN INSERT (sector_id, nombre, salario_sugerido) VALUES (x.sector_id, x.nombre, p_salario);
  END;
BEGIN
  SELECT id INTO v_otro FROM sg_sector WHERE nombre = 'Otro';
  cargo('Cajero', 1750905);            cargo('Asesor comercial', 1900000);
  cargo('Auxiliar contable', 2100000); cargo('Contador', 3200000);
  cargo('Recepcionista', 1750905);     cargo('Servicios generales', 1750905);
  cargo('Mensajero', 1750905);         cargo('Domiciliario', 1750905);
  cargo('Conductor', 2100000);         cargo('Auxiliar de bodega', 1750905);
  cargo('Supervisor', 2600000);
  -- Ninguna sugerencia por debajo del minimo legal
  UPDATE sg_cargo_plantilla SET salario_sugerido = fn_sg_salario_minimo WHERE salario_sugerido < fn_sg_salario_minimo;
  COMMIT;
  DBMS_OUTPUT.PUT_LINE('OK  cargos sugeridos del sector Otro');
END;
/

-- ---------- vistas ----------
CREATE OR REPLACE VIEW vw_sg_emprendimientos AS
SELECT e.id,
       e.usuario_id,
       e.nombre,
       e.descripcion,
       e.sector_id,
       CASE WHEN e.sector_otro IS NOT NULL THEN e.sector_otro ELSE s.nombre END AS sector,
       e.ciudad_codigo,
       c.nombre_completo   AS ciudad,
       e.fecha_inicio,
       e.estado,
       e.sector_otro
  FROM sg_emprendimiento e
  JOIN sg_sector s       ON s.id = e.sector_id
  JOIN vw_sg_ciudades c  ON c.codigo = e.ciudad_codigo;

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
         WHERE t.cargo_id = c.id AND t.estado = 'activo') AS trabajadores_activos,
       c.tipo_horario
  FROM sg_cargo c;

CREATE OR REPLACE VIEW vw_sg_obligaciones_socio AS
SELECT o.id,
       o.emprendimiento_id,
       o.movimiento_id,
       m.fecha        AS fecha_aporte,
       cm.nombre      AS categoria,
       o.beneficiario,
       o.monto_aporte,
       o.porcentaje,
       o.monto,
       o.estado,
       o.fecha_pago,
       o.movimiento_pago_id,
       o.fecha_limite
  FROM sg_obligacion_socio o
  JOIN sg_movimiento m            ON m.id = o.movimiento_id
  JOIN sg_categoria_movimiento cm ON cm.id = m.categoria_id;

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
           AND TRUNC(v.fecha, 'MM') = TRUNC(fn_sg_ahora, 'MM'))               AS ventas_del_mes,
       (SELECT COUNT(*) FROM sg_postulacion po JOIN sg_vacante va ON va.id = po.vacante_id
         WHERE va.emprendimiento_id = e.id AND po.estado = 'recibida')    AS postulaciones_nuevas,
       fn_sg_balance(e.id)                                                AS balance
  FROM sg_emprendimiento e
  JOIN sg_sector s ON s.id = e.sector_id;

-- ---------- reportes y procedimientos con la hora de Colombia ----------
CREATE OR REPLACE PACKAGE BODY pkg_sg_reportes AS

  FUNCTION total_ventas (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN total_ventas_rango(p_emprendimiento_id, DATE '1900-01-01', DATE '9999-12-31');
  END total_ventas;

  FUNCTION total_ventas_rango (p_emprendimiento_id IN NUMBER, p_desde IN DATE, p_hasta IN DATE) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT NVL(SUM(d.cantidad * d.precio_unitario), 0) INTO v_total
      FROM sg_detalle_venta d
      JOIN sg_venta v ON v.id = d.venta_id
     WHERE v.emprendimiento_id = p_emprendimiento_id
       AND TRUNC(v.fecha) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta);
    RETURN v_total;
  END total_ventas_rango;

  FUNCTION total_ingresos (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT NVL(SUM(monto), 0) INTO v_total
      FROM sg_movimiento
     WHERE emprendimiento_id = p_emprendimiento_id
       AND tipo IN ('Venta', 'Ingreso');
    RETURN v_total;
  END total_ingresos;

  FUNCTION total_gastos (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT NVL(SUM(monto), 0) INTO v_total
      FROM sg_movimiento
     WHERE emprendimiento_id = p_emprendimiento_id
       AND tipo IN ('Compra', 'Gasto');
    RETURN v_total;
  END total_gastos;

  FUNCTION utilidad (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN fn_sg_balance(p_emprendimiento_id);
  END utilidad;

  FUNCTION valor_inventario (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT NVL(SUM(valor_inventario), 0) INTO v_total
      FROM vw_sg_margen_producto
     WHERE emprendimiento_id = p_emprendimiento_id;
    RETURN v_total;
  END valor_inventario;

  PROCEDURE top_productos (p_emprendimiento_id IN NUMBER, p_limite IN NUMBER, p_resultado OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_resultado FOR
      SELECT nombre, unidades, ingresos
        FROM (SELECT d.producto_nombre AS nombre,
                     SUM(d.cantidad) AS unidades,
                     SUM(d.cantidad * d.precio_unitario) AS ingresos
                FROM sg_detalle_venta d
                JOIN sg_venta v ON v.id = d.venta_id
               WHERE v.emprendimiento_id = p_emprendimiento_id
               GROUP BY d.producto_nombre
               ORDER BY unidades DESC, ingresos DESC)
       WHERE ROWNUM <= NVL(p_limite, 5);
  END top_productos;

  -- Ventas de los ultimos p_meses meses (incluye los meses sin ventas, en 0)
  PROCEDURE ventas_por_mes (p_emprendimiento_id IN NUMBER, p_meses IN NUMBER, p_resultado OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_resultado FOR
      WITH meses AS (
        SELECT ADD_MONTHS(TRUNC(fn_sg_ahora, 'MM'), -(LEVEL - 1)) AS inicio
          FROM dual
        CONNECT BY LEVEL <= NVL(p_meses, 6)
      )
      SELECT TO_CHAR(m.inicio, 'YYYY-MM') AS mes,
             pkg_sg_reportes.total_ventas_rango(p_emprendimiento_id, m.inicio, LAST_DAY(m.inicio)) AS total
        FROM meses m
       ORDER BY m.inicio;
  END ventas_por_mes;

  PROCEDURE gastos_por_categoria (p_emprendimiento_id IN NUMBER, p_mes IN VARCHAR2, p_resultado OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_resultado FOR
      SELECT categoria, SUM(monto) AS total
        FROM vw_sg_movimientos
       WHERE emprendimiento_id = p_emprendimiento_id
         AND es_gasto = 'S'
         AND (p_mes IS NULL OR TO_CHAR(fecha, 'YYYY-MM') = p_mes)
       GROUP BY categoria
       ORDER BY total DESC;
  END gastos_por_categoria;

END pkg_sg_reportes;
/

CREATE OR REPLACE PROCEDURE sp_sg_registrar_venta (
  p_emprendimiento_id IN  NUMBER,
  p_observacion       IN  VARCHAR2,
  p_detalles          IN  CLOB,
  p_venta_id          OUT NUMBER
) IS
  v_lineas    NUMBER := 0;
  v_total     NUMBER := 0;
  v_producto  sg_producto%ROWTYPE;
BEGIN
  SAVEPOINT sp_venta;

  IF p_detalles IS NULL OR DBMS_LOB.GETLENGTH(p_detalles) < 2 THEN
    RAISE_APPLICATION_ERROR(-20201, 'Agrega al menos un producto a la venta.');
  END IF;

  INSERT INTO sg_venta (emprendimiento_id, fecha, observacion)
  VALUES (p_emprendimiento_id, fn_sg_ahora, TRIM(p_observacion))
  RETURNING id INTO p_venta_id;

  -- Si el mismo producto viene dos veces, se suman las cantidades (GROUP BY)
  FOR d IN (SELECT jt.producto_id, SUM(jt.cantidad) AS cantidad
              FROM JSON_TABLE(p_detalles, '$[*]'
                     COLUMNS (producto_id NUMBER PATH '$.producto_id',
                              cantidad    NUMBER PATH '$.cantidad')) jt
             GROUP BY jt.producto_id) LOOP

    IF d.cantidad IS NULL OR d.cantidad <= 0 OR d.cantidad <> TRUNC(d.cantidad) THEN
      RAISE_APPLICATION_ERROR(-20202, 'La cantidad de cada producto debe ser un número entero mayor que cero.');
    END IF;

    BEGIN
      -- FOR UPDATE bloquea la fila: dos ventas simultaneas no pueden vender el mismo stock
      SELECT * INTO v_producto
        FROM sg_producto
       WHERE id = d.producto_id
         AND emprendimiento_id = p_emprendimiento_id
         FOR UPDATE;
    EXCEPTION
      WHEN NO_DATA_FOUND THEN
        RAISE_APPLICATION_ERROR(-20203, 'Un producto de la venta ya no existe.');
    END;

    IF v_producto.stock < d.cantidad THEN
      RAISE_APPLICATION_ERROR(-20204, 'Stock insuficiente de "' || v_producto.nombre ||
                                      '". Disponible: ' || v_producto.stock || '.');
    END IF;

    INSERT INTO sg_detalle_venta (venta_id, producto_id, producto_nombre, cantidad, precio_unitario)
    VALUES (p_venta_id, v_producto.id, v_producto.nombre, d.cantidad, v_producto.precio_venta);

    UPDATE sg_producto SET stock = stock - d.cantidad WHERE id = v_producto.id;

    v_lineas := v_lineas + 1;
    v_total  := v_total + d.cantidad * v_producto.precio_venta;
  END LOOP;

  IF v_lineas = 0 THEN
    RAISE_APPLICATION_ERROR(-20201, 'Agrega al menos un producto a la venta.');
  END IF;

  INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto, referencia)
  VALUES (p_emprendimiento_id, fn_sg_ahora, 'Venta', fn_sg_categoria_sistema('Ventas', 'Ingreso'),
          'Venta #' || p_venta_id, v_total, 'venta-' || p_venta_id);
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK TO sp_venta;
    RAISE;
END sp_sg_registrar_venta;
/

CREATE OR REPLACE PROCEDURE sp_sg_mover_stock (
  p_producto_id IN NUMBER,
  p_cantidad    IN NUMBER,
  p_tipo        IN VARCHAR2      -- 'ENTRADA' o 'SALIDA'
) IS
  v_producto sg_producto%ROWTYPE;
  v_monto    NUMBER;
BEGIN
  IF p_tipo NOT IN ('ENTRADA', 'SALIDA') THEN
    RAISE_APPLICATION_ERROR(-20301, 'El tipo debe ser ENTRADA o SALIDA.');
  END IF;
  IF p_cantidad IS NULL OR p_cantidad <= 0 OR p_cantidad <> TRUNC(p_cantidad) THEN
    RAISE_APPLICATION_ERROR(-20302, 'La cantidad debe ser un número entero mayor que cero.');
  END IF;

  BEGIN
    SELECT * INTO v_producto FROM sg_producto WHERE id = p_producto_id FOR UPDATE;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      RAISE_APPLICATION_ERROR(-20303, 'Producto no encontrado.');
  END;

  IF p_tipo = 'SALIDA' AND v_producto.stock < p_cantidad THEN
    RAISE_APPLICATION_ERROR(-20304, 'Stock insuficiente. Disponible: ' || v_producto.stock || '.');
  END IF;

  UPDATE sg_producto
     SET stock = stock + CASE p_tipo WHEN 'ENTRADA' THEN p_cantidad ELSE -p_cantidad END
   WHERE id = p_producto_id;

  v_monto := v_producto.precio_compra * p_cantidad;
  IF v_monto > 0 THEN
    INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto, referencia)
    VALUES (v_producto.emprendimiento_id, fn_sg_ahora,
            CASE p_tipo WHEN 'ENTRADA' THEN 'Compra' ELSE 'AjusteStock' END,
            fn_sg_categoria_sistema('Inventario', 'Gasto'),
            CASE p_tipo WHEN 'ENTRADA' THEN 'Entrada de stock: ' ELSE 'Salida manual: ' END
              || v_producto.nombre || ' x' || p_cantidad,
            v_monto, 'producto-' || p_producto_id);
  END IF;
END sp_sg_mover_stock;
/

CREATE OR REPLACE PROCEDURE sp_sg_contratar_postulante (
  p_postulacion_id IN  NUMBER,
  p_fecha_ingreso  IN  DATE,
  p_trabajador_id  OUT NUMBER
) IS
  v_post  sg_postulacion%ROWTYPE;
  v_vac   sg_vacante%ROWTYPE;
BEGIN
  BEGIN
    SELECT * INTO v_post FROM sg_postulacion WHERE id = p_postulacion_id FOR UPDATE;
    SELECT * INTO v_vac  FROM sg_vacante     WHERE id = v_post.vacante_id;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      RAISE_APPLICATION_ERROR(-20401, 'La postulación no existe.');
  END;

  IF v_post.estado = 'aceptada' THEN
    RAISE_APPLICATION_ERROR(-20402, 'Esta postulación ya fue aceptada.');
  END IF;

  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento,
                             nombre, apellido, telefono, email, ciudad_codigo, fecha_ingreso, estado)
  VALUES (v_vac.emprendimiento_id, v_vac.cargo_id, v_post.tipo_documento, v_post.numero_documento,
          v_post.nombre, v_post.apellido, v_post.telefono, v_post.email, v_post.ciudad_codigo,
          NVL(p_fecha_ingreso, TRUNC(fn_sg_ahora)), 'activo')
  RETURNING id INTO p_trabajador_id;

  UPDATE sg_postulacion SET estado = 'aceptada' WHERE id = p_postulacion_id;
EXCEPTION
  WHEN DUP_VAL_ON_INDEX THEN
    RAISE_APPLICATION_ERROR(-20403, 'Ya existe un trabajador con ese documento en el emprendimiento.');
END sp_sg_contratar_postulante;
/

CREATE OR REPLACE PROCEDURE sp_sg_pagar_obligacion (
    p_emprendimiento_id IN NUMBER,
    p_obligacion_id     IN NUMBER,
    p_fecha             IN DATE,
    p_movimiento_id     OUT NUMBER
) AS
    v_o     sg_obligacion_socio%ROWTYPE;
    v_cat   sg_categoria_movimiento.nombre%TYPE;
    v_aport sg_movimiento.fecha%TYPE;
    v_fecha DATE := NVL(p_fecha, fn_sg_ahora);
BEGIN
    BEGIN
        SELECT * INTO v_o FROM sg_obligacion_socio
         WHERE id = p_obligacion_id AND emprendimiento_id = p_emprendimiento_id
           FOR UPDATE;   -- dos personas no la pagan dos veces
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20144, 'Esa devolución no existe en este emprendimiento.');
    END;
    IF v_o.estado = 'pagada' THEN
        RAISE_APPLICATION_ERROR(-20145, 'Esa devolución ya está registrada como gasto.');
    END IF;

    SELECT m.fecha, cm.nombre INTO v_aport, v_cat
      FROM sg_movimiento m JOIN sg_categoria_movimiento cm ON cm.id = m.categoria_id
     WHERE m.id = v_o.movimiento_id;
    IF TRUNC(v_fecha) < TRUNC(v_aport) THEN
        RAISE_APPLICATION_ERROR(-20146, 'La devolución no puede ser anterior al aporte (' || TO_CHAR(v_aport, 'DD/MM/YYYY') || ').');
    END IF;

    INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto, referencia)
    VALUES (p_emprendimiento_id, v_fecha, 'Gasto', fn_sg_categoria_sistema('Devolución a socios', 'Gasto'),
            SUBSTR('Devolución a ' || v_o.beneficiario || ' (' || RTRIM(TO_CHAR(v_o.porcentaje, 'FM99990.99'), '.')
                   || ' % de ' || LOWER(v_cat) || ')', 1, 300),
            v_o.monto, 'DEVOLUCION-' || v_o.id)
    RETURNING id INTO p_movimiento_id;

    UPDATE sg_obligacion_socio
       SET estado = 'pagada', fecha_pago = v_fecha, movimiento_pago_id = p_movimiento_id
     WHERE id = v_o.id;
END sp_sg_pagar_obligacion;
/

CREATE OR REPLACE PROCEDURE sp_sg_registrar_aporte (
    p_emprendimiento_id IN NUMBER,
    p_categoria_id      IN NUMBER,
    p_descripcion       IN VARCHAR2,
    p_monto             IN NUMBER,
    p_fecha             IN DATE,
    p_beneficiario      IN VARCHAR2,
    p_porcentaje        IN NUMBER,
    p_gasto_inmediato   IN VARCHAR2,      -- 'S': se registra ya como gasto; 'N': queda pendiente
    p_movimiento_id     OUT NUMBER,
    p_obligacion_id     OUT NUMBER,
    p_fecha_limite      IN  DATE DEFAULT NULL  -- hasta cuando hay que devolverle (opcional)
) AS
    v_cat   sg_categoria_movimiento.nombre%TYPE;
    v_tipo  sg_categoria_movimiento.tipo%TYPE;
    v_benef sg_obligacion_socio.beneficiario%TYPE := TRIM(p_beneficiario);
    v_fecha DATE := NVL(p_fecha, fn_sg_ahora);
    v_pago  NUMBER;
BEGIN
    BEGIN
        SELECT nombre, tipo INTO v_cat, v_tipo FROM sg_categoria_movimiento WHERE id = p_categoria_id;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_cat := NULL;
    END;
    IF v_cat IS NULL OR v_tipo <> 'Ingreso' OR v_cat NOT IN ('Aportes de socios', 'Inversión', 'Préstamos recibidos') THEN
        RAISE_APPLICATION_ERROR(-20141, 'El porcentaje a devolver solo aplica a aportes de socios, inversiones y préstamos.');
    END IF;
    IF v_benef IS NULL THEN
        RAISE_APPLICATION_ERROR(-20142, 'Escribe el nombre del socio o inversionista.');
    END IF;
    IF p_porcentaje IS NULL OR p_porcentaje <= 0 OR p_porcentaje > 500 THEN
        RAISE_APPLICATION_ERROR(-20143, 'El porcentaje a devolver debe ser mayor que 0 y máximo 500.');
    END IF;
    IF p_monto IS NULL OR p_monto <= 0 THEN
        RAISE_APPLICATION_ERROR(-20147, 'El monto debe ser mayor que cero.');
    END IF;
    IF p_fecha_limite IS NOT NULL AND TRUNC(p_fecha_limite) < TRUNC(v_fecha) THEN
        RAISE_APPLICATION_ERROR(-20148, 'La fecha para devolver no puede ser anterior al aporte.');
    END IF;

    INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto)
    VALUES (p_emprendimiento_id, v_fecha, 'Ingreso', p_categoria_id,
            SUBSTR(NVL(TRIM(p_descripcion), v_cat || ' de ' || v_benef), 1, 300), ROUND(p_monto, 2))
    RETURNING id INTO p_movimiento_id;

    INSERT INTO sg_obligacion_socio (emprendimiento_id, movimiento_id, beneficiario, monto_aporte, porcentaje, monto, fecha_limite)
    VALUES (p_emprendimiento_id, p_movimiento_id, v_benef, ROUND(p_monto, 2), p_porcentaje,
            ROUND(p_monto * p_porcentaje / 100, 2), TRUNC(p_fecha_limite))
    RETURNING id INTO p_obligacion_id;

    IF UPPER(NVL(p_gasto_inmediato, 'N')) = 'S' THEN
        sp_sg_pagar_obligacion(p_emprendimiento_id, p_obligacion_id, v_fecha, v_pago);
    END IF;
END sp_sg_registrar_aporte;
/

-- ---------- disparadores ----------
CREATE OR REPLACE TRIGGER trg_sg_cargo_valida
BEFORE INSERT OR UPDATE ON sg_cargo
FOR EACH ROW
DECLARE
  v_minimo NUMBER;
BEGIN
  :NEW.nombre := TRIM(:NEW.nombre);
  IF UPDATING AND UPPER(:NEW.nombre) <> UPPER(:OLD.nombre) THEN
    RAISE_APPLICATION_ERROR(-20113, 'El nombre del cargo no se puede cambiar. Crea un cargo nuevo si lo necesitas.');
  END IF;
  IF INSERTING OR :NEW.salario_base <> :OLD.salario_base OR :NEW.tipo_horario <> :OLD.tipo_horario THEN
    v_minimo := fn_sg_salario_minimo(:NEW.tipo_horario);
    IF :NEW.salario_base < v_minimo THEN
      RAISE_APPLICATION_ERROR(-20114, 'El salario para ' || LOWER(:NEW.tipo_horario) || ' debe ser de al menos $' ||
                                      TRIM(TO_CHAR(v_minimo, '999G999G999', 'NLS_NUMERIC_CHARACTERS='',.''')) || '.');
    END IF;
  END IF;
END trg_sg_cargo_valida;
/

CREATE OR REPLACE TRIGGER trg_sg_emprendimiento_sector
BEFORE INSERT OR UPDATE OF sector_id, sector_otro ON sg_emprendimiento
FOR EACH ROW
DECLARE
  v_sector sg_sector.nombre%TYPE;
BEGIN
  SELECT nombre INTO v_sector FROM sg_sector WHERE id = :NEW.sector_id;
  IF v_sector = 'Otro' THEN
    :NEW.sector_otro := TRIM(:NEW.sector_otro);
    IF :NEW.sector_otro IS NULL THEN
      RAISE_APPLICATION_ERROR(-20115, 'Escribe a qué sector se dedica el emprendimiento.');
    END IF;
  ELSE
    :NEW.sector_otro := NULL;
  END IF;
END trg_sg_emprendimiento_sector;
/

-- ---------- permisos ----------
DECLARE
  PROCEDURE dar(p_usuario VARCHAR2, p_privilegio VARCHAR2, p_objeto VARCHAR2) IS
    v_existe NUMBER;
  BEGIN
    SELECT COUNT(*) INTO v_existe FROM all_users WHERE username = p_usuario;
    IF v_existe = 0 THEN RETURN; END IF;
    EXECUTE IMMEDIATE 'GRANT ' || p_privilegio || ' ON ' || p_objeto || ' TO ' || p_usuario;
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM ' || p_usuario || '.' || p_objeto || ' FOR ' || USER || '.' || p_objeto;
  END;
BEGIN
  dar('SGAPE_APP', 'EXECUTE', 'FN_SG_AHORA');
  dar('SGAPE_APP', 'EXECUTE', 'FN_SG_SALARIO_MINIMO');
  dar('SGAPE_BOT', 'EXECUTE', 'FN_SG_AHORA');
  DBMS_OUTPUT.PUT_LINE('OK  permisos');
END;
/

-- ---------- prueba (se deshace) ----------
DECLARE
  v_u NUMBER; v_e NUMBER; v_c NUMBER;
BEGIN
  SAVEPOINT prueba_mejoras;
  INSERT INTO sg_usuario (nombre, email, clave_hash) VALUES ('Prueba mejoras', 'prueba.mejoras@sgape.co', 'x:y') RETURNING id INTO v_u;
  BEGIN
    INSERT INTO sg_emprendimiento (usuario_id, nombre, sector_id, ciudad_codigo, fecha_inicio)
    VALUES (v_u, 'Sin sector', (SELECT id FROM sg_sector WHERE nombre = 'Otro'), '20001', SYSDATE - 1);
    DBMS_OUTPUT.PUT_LINE('FALLA sector Otro sin nombre');
  EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -20115 THEN DBMS_OUTPUT.PUT_LINE('OK    sector Otro pide el nombre'); ELSE RAISE; END IF;
  END;
  INSERT INTO sg_emprendimiento (usuario_id, nombre, sector_id, sector_otro, ciudad_codigo, fecha_inicio)
  VALUES (v_u, 'Con sector', (SELECT id FROM sg_sector WHERE nombre = 'Otro'), 'Mascotas', '20001', SYSDATE - 1) RETURNING id INTO v_e;
  BEGIN
    INSERT INTO sg_cargo (emprendimiento_id, nombre, salario_base) VALUES (v_e, 'Vendedor', 1);
    DBMS_OUTPUT.PUT_LINE('FALLA salario de $1 aceptado');
  EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -20114 THEN DBMS_OUTPUT.PUT_LINE('OK    salario de $1 rechazado'); ELSE RAISE; END IF;
  END;
  INSERT INTO sg_cargo (emprendimiento_id, nombre, salario_base, tipo_horario) VALUES (v_e, 'Vendedor', 900000, 'Medio tiempo') RETURNING id INTO v_c;
  DBMS_OUTPUT.PUT_LINE('OK    medio tiempo con medio salario minimo');
  BEGIN
    UPDATE sg_cargo SET nombre = 'Gerente' WHERE id = v_c;
    DBMS_OUTPUT.PUT_LINE('FALLA cambio de nombre aceptado');
  EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -20113 THEN DBMS_OUTPUT.PUT_LINE('OK    el nombre del cargo no se cambia'); ELSE RAISE; END IF;
  END;
  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, fecha_ingreso)
  VALUES (v_e, v_c, 'CC', '1065123456', 'Ana', 'Pérez', SYSDATE - 1);
  BEGIN
    INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, fecha_ingreso)
    VALUES (v_e, v_c, 'CE', '1065123456', 'Otra', 'Persona', SYSDATE - 1);
    DBMS_OUTPUT.PUT_LINE('FALLA mismo numero con otro tipo aceptado');
  EXCEPTION WHEN DUP_VAL_ON_INDEX THEN DBMS_OUTPUT.PUT_LINE('OK    mismo numero con otro tipo rechazado');
  END;
  IF TRUNC(fn_sg_ahora) = TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'America/Bogota' AS DATE)) THEN
    DBMS_OUTPUT.PUT_LINE('OK    hora de Colombia: ' || TO_CHAR(fn_sg_ahora, 'YYYY-MM-DD HH24:MI') || ' (UTC: ' || TO_CHAR(SYSDATE, 'HH24:MI') || ')');
  END IF;
  ROLLBACK TO prueba_mejoras;
  DBMS_OUTPUT.PUT_LINE('Prueba terminada (datos deshechos).');
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK TO prueba_mejoras;
    DBMS_OUTPUT.PUT_LINE('ERROR EN LA PRUEBA: ' || SQLERRM);
END;
/

SELECT object_name, object_type, status FROM user_objects
 WHERE status <> 'VALID' AND (object_name LIKE '%SG\_%' ESCAPE '\');
SELECT 'objetos invalidos: ' || COUNT(*) AS revision FROM user_objects WHERE status <> 'VALID' AND object_name LIKE '%SG\_%' ESCAPE '\';
