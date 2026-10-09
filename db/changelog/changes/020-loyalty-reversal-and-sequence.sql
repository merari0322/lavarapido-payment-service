--liquibase formatted sql
-- Ledger de puntos:
--   1. REVERSED: tipo de movimiento con el que se devuelven los puntos ganados por una reserva
--      cuando su pago se reembolsa (signo -1).
--   2. sequence_no: posicion de cada movimiento en el ledger de su cliente (1, 2, 3...). Es unica
--      por cliente, asi dos movimientos calculados a la vez sobre el mismo saldo no pueden
--      guardarse los dos (el segundo choca y el servicio responde 409 en vez de dejar un
--      balance_after equivocado).

--changeset merari:pay-020-loyalty-movement-reversed dbms:mssql
--comment: REVERSED = puntos revertidos por reembolso del pago
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM [payment].loyalty_movement_type WHERE code = N'REVERSED'
INSERT INTO [payment].loyalty_movement_type (code, name, sign, display_order) VALUES (N'REVERSED', N'Puntos revertidos', -1, 5);
--rollback DELETE FROM [payment].loyalty_movement_type WHERE code = N'REVERSED';

--changeset merari:pay-020-loyalty-sequence-column dbms:mssql
--comment: Posicion del movimiento en el ledger del cliente
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('payment.loyalty_transaction') AND name = 'sequence_no'
ALTER TABLE [payment].loyalty_transaction ADD sequence_no INT NULL;
--rollback ALTER TABLE [payment].loyalty_transaction DROP COLUMN sequence_no;

--changeset merari:pay-020-loyalty-sequence-backfill dbms:mssql
--comment: Numera los movimientos existentes en el orden en que se registraron
UPDATE t
SET t.sequence_no = n.rn
FROM [payment].loyalty_transaction t
JOIN (
    SELECT loyalty_transaction_id,
           ROW_NUMBER() OVER (PARTITION BY customer_id ORDER BY loyalty_transaction_id) AS rn
    FROM [payment].loyalty_transaction
) n ON n.loyalty_transaction_id = t.loyalty_transaction_id;
--rollback SELECT 1;

--changeset merari:pay-020-loyalty-sequence-not-null dbms:mssql
--comment: Todo movimiento tiene su posicion
ALTER TABLE [payment].loyalty_transaction ALTER COLUMN sequence_no INT NOT NULL;
--rollback ALTER TABLE [payment].loyalty_transaction ALTER COLUMN sequence_no INT NULL;

--changeset merari:pay-020-loyalty-sequence-unique dbms:mssql
--comment: Una sola fila por posicion y cliente (concurrencia del saldo)
CREATE UNIQUE INDEX ux_loyalty_transaction_customer_sequence ON [payment].loyalty_transaction (customer_id, sequence_no);
--rollback DROP INDEX ux_loyalty_transaction_customer_sequence ON [payment].loyalty_transaction;
