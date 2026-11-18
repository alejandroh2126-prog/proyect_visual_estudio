/* =====================================================================
   PARTE 6. DISPARADORES (se ejecutan solos con cada INSERT/UPDATE/DELETE
   que haga la aplicacion o el bot: son la ultima barrera de validacion)
   ===================================================================== */

-- 6.1 Auditoria: guarda cada cambio de precio de compra o de venta (usa :OLD y :NEW)
CREATE OR REPLACE TRIGGER trg_sg_producto_precio_aud
AFTER UPDATE OF precio_compra, precio_venta ON sg_producto
FOR EACH ROW
WHEN (OLD.precio_compra <> NEW.precio_compra OR OLD.precio_venta <> NEW.precio_venta)
BEGIN
  INSERT INTO sg_auditoria_precio
    (producto_id, precio_compra_ant, precio_compra_nuevo, precio_venta_ant, precio_venta_nuevo, usuario_bd)
  VALUES
    (:OLD.id, :OLD.precio_compra, :NEW.precio_compra, :OLD.precio_venta, :NEW.precio_venta, USER);
END trg_sg_producto_precio_aud;
/

-- 6.2 Bitacora de ventas
CREATE OR REPLACE TRIGGER trg_sg_venta_bitacora
AFTER INSERT OR DELETE ON sg_venta
FOR EACH ROW
BEGIN
  IF INSERTING THEN
    INSERT INTO sg_bitacora (tabla, operacion, registro_id, detalle, usuario_bd)
    VALUES ('SG_VENTA', 'INSERT', :NEW.id, 'Venta registrada en el emprendimiento ' || :NEW.emprendimiento_id, USER);
  ELSE
    INSERT INTO sg_bitacora (tabla, operacion, registro_id, detalle, usuario_bd)
    VALUES ('SG_VENTA', 'DELETE', :OLD.id, 'Venta eliminada del emprendimiento ' || :OLD.emprendimiento_id, USER);
  END IF;
END trg_sg_venta_bitacora;
/

-- 6.3 Bitacora de emprendimientos: crear, cambiar de estado o eliminar
CREATE OR REPLACE TRIGGER trg_sg_emprendimiento_bitacora
AFTER INSERT OR UPDATE OF estado OR DELETE ON sg_emprendimiento
FOR EACH ROW
BEGIN
  IF INSERTING THEN
    INSERT INTO sg_bitacora (tabla, operacion, registro_id, detalle, usuario_bd)
    VALUES ('SG_EMPRENDIMIENTO', 'INSERT', :NEW.id, SUBSTR('Emprendimiento creado: ' || :NEW.nombre, 1, 200), USER);
  ELSIF UPDATING THEN
    IF :OLD.estado <> :NEW.estado THEN
      INSERT INTO sg_bitacora (tabla, operacion, registro_id, detalle, usuario_bd)
      VALUES ('SG_EMPRENDIMIENTO', 'UPDATE', :NEW.id, 'Estado: ' || :OLD.estado || ' -> ' || :NEW.estado, USER);
    END IF;
  ELSE
    INSERT INTO sg_bitacora (tabla, operacion, registro_id, detalle, usuario_bd)
    VALUES ('SG_EMPRENDIMIENTO', 'DELETE', :OLD.id, SUBSTR('Emprendimiento eliminado: ' || :OLD.nombre, 1, 200), USER);
  END IF;
END trg_sg_emprendimiento_bitacora;
/

-- 6.4 Presupuesto: mes con formato yyyy-MM y solo categorias de GASTO
CREATE OR REPLACE TRIGGER trg_sg_presupuesto_valida
BEFORE INSERT OR UPDATE ON sg_presupuesto
FOR EACH ROW
DECLARE
  v_tipo sg_categoria_movimiento.tipo%TYPE;
BEGIN
  IF NOT REGEXP_LIKE(:NEW.mes, '^[0-9]{4}-(0[1-9]|1[0-2])$') THEN
    RAISE_APPLICATION_ERROR(-20101, 'El mes debe tener formato aaaa-mm, por ejemplo 2026-11.');
  END IF;

  SELECT tipo INTO v_tipo FROM sg_categoria_movimiento WHERE id = :NEW.categoria_id;
  IF v_tipo <> 'Gasto' THEN
    RAISE_APPLICATION_ERROR(-20102, 'Los presupuestos solo se definen para categorías de gasto.');
  END IF;
END trg_sg_presupuesto_valida;
/

-- 6.5 Usuario: correo sin espacios y en minuscula, nombre sin espacios sobrantes
CREATE OR REPLACE TRIGGER trg_sg_usuario_email
BEFORE INSERT OR UPDATE OF email, nombre ON sg_usuario
FOR EACH ROW
BEGIN
  :NEW.email  := LOWER(TRIM(:NEW.email));
  :NEW.nombre := TRIM(:NEW.nombre);
END trg_sg_usuario_email;
/

-- 6.6 Trabajador: documento valido segun su tipo y cargo del MISMO emprendimiento
CREATE OR REPLACE TRIGGER trg_sg_trabajador_valida
BEFORE INSERT OR UPDATE ON sg_trabajador
FOR EACH ROW
DECLARE
  v_emp_cargo sg_cargo.emprendimiento_id%TYPE;
BEGIN
  :NEW.numero_documento := UPPER(REPLACE(REPLACE(TRIM(:NEW.numero_documento), '.', ''), ' ', ''));
  :NEW.nombre   := TRIM(:NEW.nombre);
  :NEW.apellido := TRIM(:NEW.apellido);
  :NEW.email    := LOWER(TRIM(:NEW.email));

  IF fn_sg_documento_valido(:NEW.tipo_documento, :NEW.numero_documento) = 0 THEN
    RAISE_APPLICATION_ERROR(-20111, 'El número de documento no es válido para el tipo ' || :NEW.tipo_documento || '.');
  END IF;

  SELECT emprendimiento_id INTO v_emp_cargo FROM sg_cargo WHERE id = :NEW.cargo_id;
  IF v_emp_cargo <> :NEW.emprendimiento_id THEN
    RAISE_APPLICATION_ERROR(-20112, 'El cargo elegido pertenece a otro emprendimiento.');
  END IF;
END trg_sg_trabajador_valida;
/

-- 6.7 Movimiento: la categoria debe coincidir con el tipo (un ingreso no puede ir en una categoria de gasto)
CREATE OR REPLACE TRIGGER trg_sg_movimiento_valida
BEFORE INSERT OR UPDATE ON sg_movimiento
FOR EACH ROW
DECLARE
  v_tipo_cat sg_categoria_movimiento.tipo%TYPE;
BEGIN
  SELECT tipo INTO v_tipo_cat FROM sg_categoria_movimiento WHERE id = :NEW.categoria_id;

  IF (:NEW.tipo IN ('Venta', 'Ingreso') AND v_tipo_cat <> 'Ingreso')
  OR (:NEW.tipo IN ('Compra', 'Gasto', 'AjusteStock') AND v_tipo_cat <> 'Gasto') THEN
    RAISE_APPLICATION_ERROR(-20121, 'La categoría no corresponde al tipo de movimiento (' || :NEW.tipo || ').');
  END IF;

  IF :NEW.fecha > SYSDATE + 1 THEN
    RAISE_APPLICATION_ERROR(-20122, 'La fecha del movimiento no puede ser futura.');
  END IF;
END trg_sg_movimiento_valida;
/

-- 6.8 Postulacion: documento valido y solo a vacantes abiertas
CREATE OR REPLACE TRIGGER trg_sg_postulacion_valida
BEFORE INSERT OR UPDATE OF tipo_documento, numero_documento ON sg_postulacion
FOR EACH ROW
DECLARE
  v_estado sg_vacante.estado%TYPE;
BEGIN
  :NEW.numero_documento := UPPER(REPLACE(REPLACE(TRIM(:NEW.numero_documento), '.', ''), ' ', ''));
  :NEW.email := LOWER(TRIM(:NEW.email));

  IF fn_sg_documento_valido(:NEW.tipo_documento, :NEW.numero_documento) = 0 THEN
    RAISE_APPLICATION_ERROR(-20131, 'El número de documento no es válido para el tipo ' || :NEW.tipo_documento || '.');
  END IF;

  IF INSERTING THEN
    SELECT estado INTO v_estado FROM sg_vacante WHERE id = :NEW.vacante_id;
    IF v_estado <> 'abierta' THEN
      RAISE_APPLICATION_ERROR(-20132, 'La vacante ya está cerrada.');
    END IF;
  END IF;
END trg_sg_postulacion_valida;
/

-- 6.9 Vacante: el cargo debe ser del mismo emprendimiento
CREATE OR REPLACE TRIGGER trg_sg_vacante_valida
BEFORE INSERT OR UPDATE OF cargo_id, emprendimiento_id ON sg_vacante
FOR EACH ROW
DECLARE
  v_emp_cargo sg_cargo.emprendimiento_id%TYPE;
BEGIN
  SELECT emprendimiento_id INTO v_emp_cargo FROM sg_cargo WHERE id = :NEW.cargo_id;
  IF v_emp_cargo <> :NEW.emprendimiento_id THEN
    RAISE_APPLICATION_ERROR(-20141, 'El cargo de la vacante pertenece a otro emprendimiento.');
  END IF;
END trg_sg_vacante_valida;
/
