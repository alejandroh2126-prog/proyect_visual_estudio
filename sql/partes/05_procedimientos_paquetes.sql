/* =====================================================================
   PARTE 5. PROCEDIMIENTOS Y PAQUETES
   Ninguno imprime con DBMS_OUTPUT: devuelven datos con SYS_REFCURSOR o con
   parametros OUT, asi los puede usar la aplicacion C# y el bot de Telegram.
   Ninguno hace COMMIT: confirma quien los llama (la aplicacion confirma cada
   llamada; si el procedimiento falla, Oracle deshace todo lo que hizo).

   Para probar un SYS_REFCURSOR en SQL Developer (F5):
     VARIABLE rc REFCURSOR
     EXEC sp_sg_productos_stock_bajo(1, :rc)
     PRINT rc
   ===================================================================== */

-- ---------------------------------------------------------------------
-- 5.1 PROCEDIMIENTOS
-- ---------------------------------------------------------------------

-- Resumen de un emprendimiento (una fila)
CREATE OR REPLACE PROCEDURE sp_sg_resumen_emprendimiento (
  p_emprendimiento_id IN  NUMBER,
  p_resultado         OUT SYS_REFCURSOR
) IS
  v_existe NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_existe FROM sg_emprendimiento WHERE id = p_emprendimiento_id;
  IF v_existe = 0 THEN
    RAISE_APPLICATION_ERROR(-20001, 'El emprendimiento no existe.');
  END IF;

  OPEN p_resultado FOR
    SELECT r.*,
           pkg_sg_reportes.total_ingresos(r.emprendimiento_id) AS ingresos,
           pkg_sg_reportes.total_gastos(r.emprendimiento_id)   AS gastos,
           pkg_sg_nomina.nomina_mensual(r.emprendimiento_id)   AS nomina_mensual
      FROM vw_sg_resumen_emprendimiento r
     WHERE r.emprendimiento_id = p_emprendimiento_id;
END sp_sg_resumen_emprendimiento;
/

-- Productos con stock bajo, del mas urgente al menos urgente
CREATE OR REPLACE PROCEDURE sp_sg_productos_stock_bajo (
  p_emprendimiento_id IN  NUMBER,
  p_resultado         OUT SYS_REFCURSOR
) IS
BEGIN
  OPEN p_resultado FOR
    SELECT producto_id, nombre, categoria, stock, stock_minimo, faltante
      FROM vw_sg_stock_bajo
     WHERE emprendimiento_id = p_emprendimiento_id
     ORDER BY faltante DESC, nombre;
END sp_sg_productos_stock_bajo;
/

-- Sube o baja el precio de venta de TODOS los productos en un porcentaje.
-- El disparador trg_sg_producto_precio_aud deja registrado cada cambio en sg_auditoria_precio.
CREATE OR REPLACE PROCEDURE sp_sg_ajustar_precio_venta (
  p_emprendimiento_id IN  NUMBER,
  p_porcentaje        IN  NUMBER,
  p_afectados         OUT NUMBER
) IS
BEGIN
  IF p_porcentaje IS NULL OR p_porcentaje <= -100 OR p_porcentaje > 500 THEN
    RAISE_APPLICATION_ERROR(-20002, 'El porcentaje debe estar entre -99 y 500.');
  END IF;

  UPDATE sg_producto
     SET precio_venta = GREATEST(ROUND(precio_venta * (1 + p_porcentaje / 100), 0), 1)
   WHERE emprendimiento_id = p_emprendimiento_id;

  p_afectados := SQL%ROWCOUNT;
END sp_sg_ajustar_precio_venta;
/

-- Estado de los presupuestos de un mes (OK / ALERTA / EXCEDIDO)
CREATE OR REPLACE PROCEDURE sp_sg_presupuestos_estado (
  p_emprendimiento_id IN  NUMBER,
  p_mes               IN  VARCHAR2,
  p_resultado         OUT SYS_REFCURSOR
) IS
BEGIN
  IF NOT REGEXP_LIKE(p_mes, '^[0-9]{4}-(0[1-9]|1[0-2])$') THEN
    RAISE_APPLICATION_ERROR(-20101, 'El mes debe tener formato aaaa-mm, por ejemplo 2026-11.');
  END IF;

  OPEN p_resultado FOR
    SELECT presupuesto_id, categoria_id, categoria, mes, monto_limite, gastado,
           disponible, porcentaje_uso, estado
      FROM vw_sg_uso_presupuesto
     WHERE emprendimiento_id = p_emprendimiento_id
       AND mes = p_mes
     ORDER BY porcentaje_uso DESC;
END sp_sg_presupuestos_estado;
/

-- Borra de la bitacora los registros con mas de p_dias dias
CREATE OR REPLACE PROCEDURE sp_sg_limpiar_bitacora (
  p_dias       IN  NUMBER,
  p_eliminados OUT NUMBER
) IS
BEGIN
  IF p_dias IS NULL OR p_dias < 0 THEN
    RAISE_APPLICATION_ERROR(-20003, 'Los días deben ser cero o más.');
  END IF;

  DELETE FROM sg_bitacora WHERE fecha < SYSDATE - p_dias;
  p_eliminados := SQL%ROWCOUNT;
END sp_sg_limpiar_bitacora;
/

-- Cierra las vacantes cuya fecha de cierre ya paso
CREATE OR REPLACE PROCEDURE sp_sg_cerrar_vacantes_vencidas (
  p_cerradas OUT NUMBER
) IS
BEGIN
  UPDATE sg_vacante
     SET estado = 'cerrada'
   WHERE estado = 'abierta'
     AND fecha_cierre IS NOT NULL
     AND fecha_cierre < TRUNC(SYSDATE);
  p_cerradas := SQL%ROWCOUNT;
END sp_sg_cerrar_vacantes_vencidas;
/

-- VENTA COMPLETA EN UNA SOLA TRANSACCION DE LA BASE DE DATOS:
-- valida stock, guarda cabecera y detalle, descuenta stock y registra el ingreso en finanzas.
-- p_detalles es un JSON: [{"producto_id": 5, "cantidad": 2}, {"producto_id": 7, "cantidad": 1}]
-- Si algo falla (stock insuficiente, producto inexistente) se deshace TODO.
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
  VALUES (p_emprendimiento_id, SYSDATE, TRIM(p_observacion))
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
  VALUES (p_emprendimiento_id, SYSDATE, 'Venta', fn_sg_categoria_sistema('Ventas', 'Ingreso'),
          'Venta #' || p_venta_id, v_total, 'venta-' || p_venta_id);
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK TO sp_venta;
    RAISE;
END sp_sg_registrar_venta;
/

-- Entrada o salida manual de stock (stock + movimiento en finanzas, todo junto)
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
    VALUES (v_producto.emprendimiento_id, SYSDATE,
            CASE p_tipo WHEN 'ENTRADA' THEN 'Compra' ELSE 'AjusteStock' END,
            fn_sg_categoria_sistema('Inventario', 'Gasto'),
            CASE p_tipo WHEN 'ENTRADA' THEN 'Entrada de stock: ' ELSE 'Salida manual: ' END
              || v_producto.nombre || ' x' || p_cantidad,
            v_monto, 'producto-' || p_producto_id);
  END IF;
END sp_sg_mover_stock;
/

-- Contrata a un postulante: crea el trabajador con los datos de la postulacion y la marca aceptada
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
          NVL(p_fecha_ingreso, TRUNC(SYSDATE)), 'activo')
  RETURNING id INTO p_trabajador_id;

  UPDATE sg_postulacion SET estado = 'aceptada' WHERE id = p_postulacion_id;
EXCEPTION
  WHEN DUP_VAL_ON_INDEX THEN
    RAISE_APPLICATION_ERROR(-20403, 'Ya existe un trabajador con ese documento en el emprendimiento.');
END sp_sg_contratar_postulante;
/

-- ---------------------------------------------------------------------
-- 5.2 PAQUETE DE NOMINA
-- ---------------------------------------------------------------------
CREATE OR REPLACE PACKAGE pkg_sg_nomina AS
  FUNCTION trabajadores_activos (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION nomina_mensual       (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION nomina_quincenal     (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION total_deducciones    (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION nomina_neta          (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  PROCEDURE listar_nomina       (p_emprendimiento_id IN NUMBER, p_resultado OUT SYS_REFCURSOR);
END pkg_sg_nomina;
/

CREATE OR REPLACE PACKAGE BODY pkg_sg_nomina AS

  FUNCTION trabajadores_activos (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT COUNT(*) INTO v_total
      FROM sg_trabajador
     WHERE emprendimiento_id = p_emprendimiento_id
       AND estado = 'activo';
    RETURN v_total;
  END trabajadores_activos;

  FUNCTION nomina_mensual (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT NVL(SUM(salario_base), 0) INTO v_total
      FROM vw_sg_nomina
     WHERE emprendimiento_id = p_emprendimiento_id;
    RETURN v_total;
  END nomina_mensual;

  FUNCTION nomina_quincenal (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN ROUND(nomina_mensual(p_emprendimiento_id) / 2, 2);
  END nomina_quincenal;

  FUNCTION total_deducciones (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN fn_sg_deducciones(nomina_mensual(p_emprendimiento_id));
  END total_deducciones;

  FUNCTION nomina_neta (p_emprendimiento_id IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN fn_sg_salario_neto(nomina_mensual(p_emprendimiento_id));
  END nomina_neta;

  PROCEDURE listar_nomina (p_emprendimiento_id IN NUMBER, p_resultado OUT SYS_REFCURSOR) IS
  BEGIN
    OPEN p_resultado FOR
      SELECT trabajador_id, tipo_documento, numero_documento, trabajador, cargo,
             salario_base, pago_quincenal, salud, pension, deducciones, neto
        FROM vw_sg_nomina
       WHERE emprendimiento_id = p_emprendimiento_id
       ORDER BY trabajador;
  END listar_nomina;

END pkg_sg_nomina;
/

-- ---------------------------------------------------------------------
-- 5.3 PAQUETE DE REPORTES
-- ---------------------------------------------------------------------
CREATE OR REPLACE PACKAGE pkg_sg_reportes AS
  FUNCTION total_ventas       (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION total_ventas_rango (p_emprendimiento_id IN NUMBER, p_desde IN DATE, p_hasta IN DATE) RETURN NUMBER;
  FUNCTION total_ingresos     (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION total_gastos       (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION utilidad           (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  FUNCTION valor_inventario   (p_emprendimiento_id IN NUMBER) RETURN NUMBER;
  PROCEDURE top_productos       (p_emprendimiento_id IN NUMBER, p_limite IN NUMBER, p_resultado OUT SYS_REFCURSOR);
  PROCEDURE ventas_por_mes      (p_emprendimiento_id IN NUMBER, p_meses  IN NUMBER, p_resultado OUT SYS_REFCURSOR);
  PROCEDURE gastos_por_categoria(p_emprendimiento_id IN NUMBER, p_mes    IN VARCHAR2, p_resultado OUT SYS_REFCURSOR);
END pkg_sg_reportes;
/

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
        SELECT ADD_MONTHS(TRUNC(SYSDATE, 'MM'), -(LEVEL - 1)) AS inicio
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

-- ---------------------------------------------------------------------
-- 5.4 PAQUETE DE TELEGRAM (vincular un chat con un usuario de SGAPE)
-- ---------------------------------------------------------------------
CREATE OR REPLACE PACKAGE pkg_sg_telegram AS
  FUNCTION  generar_codigo        (p_usuario_id IN NUMBER) RETURN VARCHAR2;
  PROCEDURE vincular              (p_chat_id IN NUMBER, p_codigo IN VARCHAR2,
                                   p_usuario_id OUT NUMBER, p_nombre OUT VARCHAR2);
  FUNCTION  usuario_de_chat       (p_chat_id IN NUMBER) RETURN NUMBER;
  FUNCTION  emprendimiento_de_chat(p_chat_id IN NUMBER) RETURN NUMBER;
  PROCEDURE fijar_emprendimiento  (p_chat_id IN NUMBER, p_emprendimiento_id IN NUMBER);
  PROCEDURE desvincular           (p_chat_id IN NUMBER);
  FUNCTION  chats_vinculados      (p_usuario_id IN NUMBER) RETURN NUMBER;
END pkg_sg_telegram;
/

CREATE OR REPLACE PACKAGE BODY pkg_sg_telegram AS

  -- Codigo de 6 digitos que vence en 10 minutos. Borra los codigos anteriores del usuario.
  FUNCTION generar_codigo (p_usuario_id IN NUMBER) RETURN VARCHAR2 IS
    v_codigo VARCHAR2(6);
    v_intentos NUMBER := 0;
  BEGIN
    DELETE FROM sg_telegram_codigo WHERE usuario_id = p_usuario_id OR expira < SYSDATE;
    LOOP
      v_codigo := LPAD(TRUNC(DBMS_RANDOM.VALUE(0, 1000000)), 6, '0');
      BEGIN
        INSERT INTO sg_telegram_codigo (codigo, usuario_id, expira)
        VALUES (v_codigo, p_usuario_id, SYSDATE + 10 / 1440);
        EXIT;
      EXCEPTION
        WHEN DUP_VAL_ON_INDEX THEN
          v_intentos := v_intentos + 1;
          IF v_intentos > 5 THEN RAISE; END IF;
      END;
    END LOOP;
    RETURN v_codigo;
  END generar_codigo;

  PROCEDURE vincular (p_chat_id IN NUMBER, p_codigo IN VARCHAR2,
                      p_usuario_id OUT NUMBER, p_nombre OUT VARCHAR2) IS
    v_emp NUMBER;
  BEGIN
    BEGIN
      SELECT usuario_id INTO p_usuario_id
        FROM sg_telegram_codigo
       WHERE codigo = TRIM(p_codigo) AND expira >= SYSDATE;
    EXCEPTION
      WHEN NO_DATA_FOUND THEN
        RAISE_APPLICATION_ERROR(-20501, 'El código no es válido o ya venció. Genera uno nuevo en la aplicación.');
    END;

    SELECT nombre INTO p_nombre FROM sg_usuario WHERE id = p_usuario_id;

    -- Por defecto queda activo el primer emprendimiento activo del usuario
    SELECT MIN(id) INTO v_emp FROM sg_emprendimiento
     WHERE usuario_id = p_usuario_id AND estado = 'activo';

    MERGE INTO sg_telegram_vinculo t
    USING (SELECT p_chat_id AS chat_id FROM dual) s
       ON (t.chat_id = s.chat_id)
     WHEN MATCHED THEN
       UPDATE SET t.usuario_id = p_usuario_id, t.emprendimiento_id = v_emp, t.fecha_vinculo = SYSDATE
     WHEN NOT MATCHED THEN
       INSERT (chat_id, usuario_id, emprendimiento_id) VALUES (p_chat_id, p_usuario_id, v_emp);

    DELETE FROM sg_telegram_codigo WHERE codigo = TRIM(p_codigo);   -- un solo uso
  END vincular;

  FUNCTION usuario_de_chat (p_chat_id IN NUMBER) RETURN NUMBER IS
    v_usuario NUMBER;
  BEGIN
    SELECT usuario_id INTO v_usuario FROM sg_telegram_vinculo WHERE chat_id = p_chat_id;
    RETURN v_usuario;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN RETURN NULL;
  END usuario_de_chat;

  FUNCTION emprendimiento_de_chat (p_chat_id IN NUMBER) RETURN NUMBER IS
    v_emp NUMBER;
  BEGIN
    SELECT emprendimiento_id INTO v_emp FROM sg_telegram_vinculo WHERE chat_id = p_chat_id;
    RETURN v_emp;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN RETURN NULL;
  END emprendimiento_de_chat;

  -- Solo deja elegir emprendimientos que pertenezcan al usuario del chat
  PROCEDURE fijar_emprendimiento (p_chat_id IN NUMBER, p_emprendimiento_id IN NUMBER) IS
    v_ok NUMBER;
  BEGIN
    SELECT COUNT(*) INTO v_ok
      FROM sg_telegram_vinculo t
      JOIN sg_emprendimiento e ON e.usuario_id = t.usuario_id
     WHERE t.chat_id = p_chat_id AND e.id = p_emprendimiento_id;
    IF v_ok = 0 THEN
      RAISE_APPLICATION_ERROR(-20502, 'Ese emprendimiento no pertenece a tu cuenta.');
    END IF;
    UPDATE sg_telegram_vinculo SET emprendimiento_id = p_emprendimiento_id WHERE chat_id = p_chat_id;
  END fijar_emprendimiento;

  PROCEDURE desvincular (p_chat_id IN NUMBER) IS
  BEGIN
    DELETE FROM sg_telegram_vinculo WHERE chat_id = p_chat_id;
  END desvincular;

  FUNCTION chats_vinculados (p_usuario_id IN NUMBER) RETURN NUMBER IS
    v_total NUMBER;
  BEGIN
    SELECT COUNT(*) INTO v_total FROM sg_telegram_vinculo WHERE usuario_id = p_usuario_id;
    RETURN v_total;
  END chats_vinculados;

END pkg_sg_telegram;
/
