--liquibase formatted sql

--changeset merari:pay-015-seed-discount-type dbms:mssql
INSERT INTO [promotion].discount_type (code, name, display_order) VALUES (N'PERCENT', N'Porcentaje', 1);
INSERT INTO [promotion].discount_type (code, name, display_order) VALUES (N'FIXED',   N'Valor fijo', 2);
--rollback DELETE FROM [promotion].discount_type WHERE code IN (N'PERCENT', N'FIXED');

--changeset merari:pay-015-seed-payment-method-type dbms:mssql
INSERT INTO [payment].payment_method_type (code, name, requires_account, requires_receipt, display_order) VALUES (N'EFECTIVO',      N'Efectivo',              0, 0, 1);
INSERT INTO [payment].payment_method_type (code, name, requires_account, requires_receipt, display_order) VALUES (N'NEQUI',         N'Nequi',                 1, 1, 2);
INSERT INTO [payment].payment_method_type (code, name, requires_account, requires_receipt, display_order) VALUES (N'DAVIPLATA',     N'Daviplata',             1, 1, 3);
INSERT INTO [payment].payment_method_type (code, name, requires_account, requires_receipt, display_order) VALUES (N'TRANSFERENCIA', N'Transferencia bancaria', 1, 1, 4);
--rollback DELETE FROM [payment].payment_method_type WHERE code IN (N'EFECTIVO', N'NEQUI', N'DAVIPLATA', N'TRANSFERENCIA');

--changeset merari:pay-015-seed-payment-account-cash dbms:mssql
--comment: Cash gets an account row (no QR) so payment.payment_account_id is never special-cased
INSERT INTO [payment].payment_account (payment_method_type_id, account_holder, instructions)
SELECT payment_method_type_id, N'Caja del establecimiento', N'Pago en efectivo en el establecimiento'
FROM [payment].payment_method_type WHERE code = N'EFECTIVO';
--rollback DELETE FROM [payment].payment_account WHERE payment_method_type_id = (SELECT payment_method_type_id FROM [payment].payment_method_type WHERE code = N'EFECTIVO');

--changeset merari:pay-015-seed-payment-status dbms:mssql
--comment: One INSERT per row so IDs are 1..5 in this exact order. APPROVED must be 3.
INSERT INTO [payment].payment_status (code, name, is_final, display_order) VALUES (N'PENDING',   N'Pendiente',   0, 1);
INSERT INTO [payment].payment_status (code, name, is_final, display_order) VALUES (N'IN_REVIEW', N'En revisión', 0, 2);
INSERT INTO [payment].payment_status (code, name, is_final, display_order) VALUES (N'APPROVED',  N'Aprobado',    0, 3);
INSERT INTO [payment].payment_status (code, name, is_final, display_order) VALUES (N'REJECTED',  N'Rechazado',   1, 4);
INSERT INTO [payment].payment_status (code, name, is_final, display_order) VALUES (N'REFUNDED',  N'Reembolsado', 1, 5);
--rollback DELETE FROM [payment].payment_status WHERE code IN (N'PENDING', N'IN_REVIEW', N'APPROVED', N'REJECTED', N'REFUNDED');

--changeset merari:pay-015-verify-approved-id-is-3 dbms:mssql
--comment: Guard - halts the migration if APPROVED is not ID 3 (the filtered unique index depends on it)
--preconditions onFail:HALT
--precondition-sql-check expectedResult:APPROVED SELECT code FROM [payment].payment_status WHERE payment_status_id = 3
SELECT 1;
--rollback SELECT 1;

--changeset merari:pay-015-seed-loyalty-movement-type dbms:mssql
--comment: ADJUSTED sign is provisional (see open question in the handoff notes)
INSERT INTO [payment].loyalty_movement_type (code, name, sign, display_order) VALUES (N'EARNED',   N'Puntos ganados',   1, 1);
INSERT INTO [payment].loyalty_movement_type (code, name, sign, display_order) VALUES (N'REDEEMED', N'Puntos canjeados', -1, 2);
INSERT INTO [payment].loyalty_movement_type (code, name, sign, display_order) VALUES (N'EXPIRED',  N'Puntos vencidos',  -1, 3);
INSERT INTO [payment].loyalty_movement_type (code, name, sign, display_order) VALUES (N'ADJUSTED', N'Ajuste manual',     1, 4);
--rollback DELETE FROM [payment].loyalty_movement_type WHERE code IN (N'EARNED', N'REDEEMED', N'EXPIRED', N'ADJUSTED');
