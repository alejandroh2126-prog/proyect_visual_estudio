/* =====================================================================
   SGAPE v2 - PLANES GRATIS / PRO (modelo de negocio freemium)
   SQL Developer, conectado como ADMIN: abrir y pulsar F5.
   Se puede ejecutar varias veces sin problema.

   - Gratis: 1 emprendimiento, inventario, ventas, finanzas y presupuestos.
   - Pro:    emprendimientos ilimitados, asistente IA, Telegram, nomina,
             vacantes y reportes PDF.
   - Al registrarse: 14 dias de Pro de prueba.
   - Pro se activa con un CODIGO (sp_sg_generar_codigos) que el usuario escribe
     en la aplicacion, seccion "Mi plan". En un negocio real el codigo lo
     entregaria la pasarela de pagos al confirmar el pago.

   Crea / cambia:
     sg_usuario.plan_tipo, sg_usuario.plan_vence
     sg_codigo_activacion
     fn_sg_plan_vigente        'PRO' o 'GRATIS' (mira la fecha de vencimiento)
     sp_sg_canjear_codigo      activa Pro con un codigo
     sp_sg_generar_codigos     el administrador crea codigos (DBMS_OUTPUT)
     trg_sg_usuario_plan       14 dias de prueba al registrarse
     trg_sg_emprendimiento_plan  el plan Gratis no crea un 2.o emprendimiento
   ===================================================================== */
SET SERVEROUTPUT ON;

-- ---------- columnas del plan en sg_usuario ----------
BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE sg_usuario ADD (
      plan_tipo  VARCHAR2(10) DEFAULT ''GRATIS'' NOT NULL,
      plan_vence DATE,
      CONSTRAINT ck_sg_usuario_plan CHECK (plan_tipo IN (''GRATIS'', ''PRO''))
  )';
  -- Quien ya estaba registrado empieza con los mismos 14 dias de prueba
  EXECUTE IMMEDIATE 'UPDATE sg_usuario SET plan_tipo = ''PRO'', plan_vence = TRUNC(SYSDATE) + 14';
  COMMIT;
  DBMS_OUTPUT.PUT_LINE('OK columnas plan_tipo y plan_vence (usuarios actuales: 14 dias de Pro)');
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE = -1430 THEN DBMS_OUTPUT.PUT_LINE('sg_usuario ya tenia las columnas del plan');
    ELSE RAISE; END IF;
END;
/

-- ---------- codigos de activacion ----------
BEGIN
  EXECUTE IMMEDIATE '
    CREATE TABLE sg_codigo_activacion (
      codigo      VARCHAR2(20) CONSTRAINT pk_sg_codigo_activacion PRIMARY KEY,
      dias        NUMBER(4)    NOT NULL,
      creado      DATE DEFAULT SYSDATE NOT NULL,
      usuario_id  NUMBER(10),
      usado_en    DATE,
      CONSTRAINT ck_sg_codigo_dias CHECK (dias BETWEEN 1 AND 3660),
      CONSTRAINT fk_sg_codigo_usuario FOREIGN KEY (usuario_id) REFERENCES sg_usuario (id) ON DELETE SET NULL
    )';
  DBMS_OUTPUT.PUT_LINE('OK tabla sg_codigo_activacion');
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE = -955 THEN DBMS_OUTPUT.PUT_LINE('sg_codigo_activacion ya existia');
    ELSE RAISE; END IF;
END;
/

COMMENT ON TABLE sg_codigo_activacion IS 'Codigos que activan el plan Pro. Los crea el administrador con sp_sg_generar_codigos.';

-- ---------- plan vigente ----------
CREATE OR REPLACE FUNCTION fn_sg_plan_vigente (p_usuario_id IN NUMBER) RETURN VARCHAR2 IS
    v_tipo  sg_usuario.plan_tipo%TYPE;
    v_vence sg_usuario.plan_vence%TYPE;
BEGIN
    SELECT plan_tipo, plan_vence INTO v_tipo, v_vence FROM sg_usuario WHERE id = p_usuario_id;
    IF v_tipo = 'PRO' AND (v_vence IS NULL OR v_vence >= TRUNC(SYSDATE)) THEN
        RETURN 'PRO';
    END IF;
    RETURN 'GRATIS';
EXCEPTION
    WHEN NO_DATA_FOUND THEN RETURN 'GRATIS';
END fn_sg_plan_vigente;
/

-- ---------- canjear un codigo ----------
CREATE OR REPLACE PROCEDURE sp_sg_canjear_codigo (
    p_usuario_id IN NUMBER,
    p_codigo     IN VARCHAR2
) AS
    v_codigo sg_codigo_activacion.codigo%TYPE := UPPER(TRIM(p_codigo));
    v_dias   sg_codigo_activacion.dias%TYPE;
    v_usado  sg_codigo_activacion.usuario_id%TYPE;
BEGIN
    BEGIN
        SELECT dias, usuario_id INTO v_dias, v_usado
          FROM sg_codigo_activacion WHERE codigo = v_codigo
           FOR UPDATE;   -- dos personas no pueden usar el mismo codigo a la vez
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20080, 'El código no es válido. Revísalo e inténtalo de nuevo.');
    END;
    IF v_usado IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20081, 'Ese código ya fue usado.');
    END IF;

    UPDATE sg_codigo_activacion SET usuario_id = p_usuario_id, usado_en = SYSDATE WHERE codigo = v_codigo;
    -- Si ya era Pro, los dias se suman a lo que le quedaba
    UPDATE sg_usuario
       SET plan_tipo  = 'PRO',
           plan_vence = GREATEST(NVL(plan_vence, TRUNC(SYSDATE)), TRUNC(SYSDATE)) + v_dias
     WHERE id = p_usuario_id;
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20083, 'Usuario no encontrado.');
    END IF;
    COMMIT;
END sp_sg_canjear_codigo;
/

-- ---------- el administrador genera codigos ----------
CREATE OR REPLACE PROCEDURE sp_sg_generar_codigos (
    p_cantidad IN NUMBER DEFAULT 5,
    p_dias     IN NUMBER DEFAULT 30
) AS
    v_codigo VARCHAR2(20);
BEGIN
    FOR i IN 1 .. LEAST(NVL(p_cantidad, 5), 100) LOOP
        v_codigo := 'PRO-' || DBMS_RANDOM.STRING('X', 4) || '-' || DBMS_RANDOM.STRING('X', 4);
        INSERT INTO sg_codigo_activacion (codigo, dias) VALUES (v_codigo, p_dias);
        DBMS_OUTPUT.PUT_LINE(v_codigo || '   (' || p_dias || ' días de Pro)');
    END LOOP;
    COMMIT;
END sp_sg_generar_codigos;
/

-- ---------- 14 dias de prueba al registrarse ----------
CREATE OR REPLACE TRIGGER trg_sg_usuario_plan
BEFORE INSERT ON sg_usuario
FOR EACH ROW
BEGIN
    IF :NEW.plan_vence IS NULL THEN
        :NEW.plan_tipo  := 'PRO';
        :NEW.plan_vence := TRUNC(SYSDATE) + 14;
    END IF;
END;
/

-- ---------- el plan Gratis tiene un solo emprendimiento ----------
CREATE OR REPLACE TRIGGER trg_sg_emprendimiento_plan
BEFORE INSERT ON sg_emprendimiento
FOR EACH ROW
DECLARE
    v_total NUMBER;
BEGIN
    IF fn_sg_plan_vigente(:NEW.usuario_id) = 'GRATIS' THEN
        SELECT COUNT(*) INTO v_total FROM sg_emprendimiento WHERE usuario_id = :NEW.usuario_id;
        IF v_total >= 1 THEN
            RAISE_APPLICATION_ERROR(-20082,
                'El plan Gratis permite 1 emprendimiento. Pasa a Pro en la sección Mi plan para crear más.');
        END IF;
    END IF;
END;
/

-- ---------- permisos: la aplicacion canjea, el bot solo consulta el plan ----------
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
  dar('SGAPE_APP', 'EXECUTE', 'FN_SG_PLAN_VIGENTE');
  dar('SGAPE_APP', 'EXECUTE', 'SP_SG_CANJEAR_CODIGO');
  dar('SGAPE_BOT', 'EXECUTE', 'FN_SG_PLAN_VIGENTE');
  -- SGAPE_APP no lee sg_codigo_activacion: solo puede canjear con el procedimiento
  BEGIN EXECUTE IMMEDIATE 'REVOKE ALL ON sg_codigo_activacion FROM sgape_app'; EXCEPTION WHEN OTHERS THEN NULL; END;
  DBMS_OUTPUT.PUT_LINE('OK permisos de planes');
END;
/

-- Verificacion
SELECT object_name, object_type, status FROM user_objects
 WHERE object_name IN ('FN_SG_PLAN_VIGENTE', 'SP_SG_CANJEAR_CODIGO', 'SP_SG_GENERAR_CODIGOS',
                       'TRG_SG_USUARIO_PLAN', 'TRG_SG_EMPRENDIMIENTO_PLAN', 'SG_CODIGO_ACTIVACION')
 ORDER BY object_type, object_name;

SELECT nombre, email, plan_tipo, plan_vence, fn_sg_plan_vigente(id) AS vigente FROM sg_usuario ORDER BY id;

/* Para crear codigos (por ejemplo 5 codigos de 30 dias):
   EXEC sp_sg_generar_codigos(5, 30);
   Los codigos salen en la pestana "Salida de DBMS" / "Salida de script". */
