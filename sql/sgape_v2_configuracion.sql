/* =====================================================================
   SGAPE v2 - CONFIGURACION COMPARTIDA (clave de Groq para todos)
   SQL Developer, conectado como ADMIN: abrir y pulsar F5.
   Se puede ejecutar varias veces sin problema.

   Crea:
     - sg_configuracion        clave / valor (hoy: GROQ_API_KEY y GROQ_MODELO)
     - sp_sg_config_guardar    inserta o actualiza un valor (lo usa la aplicacion)
     - permiso de lectura para SGAPE_BOT (si ese usuario existe)

   Despues: abre la aplicacion, seccion Asistente, pega la clave de Groq y
   pulsa "Guardar para todos" (o arranca el bot una vez con GROQ_API_KEY:
   la copia solo). Desde ese momento cualquier computador que abra
   SGAPE (y el bot) la usa sin volver a pedirla. La clave NO va en Git.
   ===================================================================== */
SET SERVEROUTPUT ON;

BEGIN
  EXECUTE IMMEDIATE '
    CREATE TABLE sg_configuracion (
      clave        VARCHAR2(50)   CONSTRAINT pk_sg_configuracion PRIMARY KEY,
      valor        VARCHAR2(4000) NOT NULL,
      actualizado  DATE DEFAULT SYSDATE NOT NULL,
      CONSTRAINT ck_sg_config_clave CHECK (clave = UPPER(clave))
    )';
  DBMS_OUTPUT.PUT_LINE('OK tabla sg_configuracion creada');
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE = -955 THEN DBMS_OUTPUT.PUT_LINE('sg_configuracion ya existia');
    ELSE RAISE; END IF;
END;
/

COMMENT ON TABLE sg_configuracion IS 'Configuracion compartida por la aplicacion y el bot (clave y modelo de Groq).';

CREATE OR REPLACE PROCEDURE sp_sg_config_guardar (
    p_clave IN VARCHAR2,
    p_valor IN VARCHAR2
) AS
    v_clave sg_configuracion.clave%TYPE := UPPER(TRIM(p_clave));
BEGIN
    IF v_clave IS NULL OR TRIM(p_valor) IS NULL THEN
        RAISE_APPLICATION_ERROR(-20070, 'La clave y el valor de la configuración son obligatorios.');
    END IF;

    MERGE INTO sg_configuracion c
    USING (SELECT v_clave AS clave FROM dual) x
       ON (c.clave = x.clave)
     WHEN MATCHED THEN UPDATE SET c.valor = TRIM(p_valor), c.actualizado = SYSDATE
     WHEN NOT MATCHED THEN INSERT (clave, valor) VALUES (x.clave, TRIM(p_valor));
    COMMIT;
END sp_sg_config_guardar;
/

-- El bot LEE la configuracion y, si arranca con GROQ_API_KEY, la copia a Oracle (sp_sg_config_guardar)
DECLARE
  v_existe NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_existe FROM all_users WHERE username = 'SGAPE_BOT';
  IF v_existe > 0 THEN
    EXECUTE IMMEDIATE 'GRANT SELECT ON sg_configuracion TO sgape_bot';
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM sgape_bot.sg_configuracion FOR ' || USER || '.sg_configuracion';
    EXECUTE IMMEDIATE 'GRANT EXECUTE ON sp_sg_config_guardar TO sgape_bot';
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM sgape_bot.sp_sg_config_guardar FOR ' || USER || '.sp_sg_config_guardar';
    DBMS_OUTPUT.PUT_LINE('OK permisos para SGAPE_BOT');
  ELSE
    DBMS_OUTPUT.PUT_LINE('SGAPE_BOT no existe todavia: el permiso se da al correr sgape_v2_usuario_bot.sql');
  END IF;
END;
/

-- Verificacion (no muestra la clave, solo si existe)
SELECT clave, CASE WHEN clave LIKE '%KEY%' THEN '(guardada)' ELSE valor END AS valor, actualizado
  FROM sg_configuracion;
