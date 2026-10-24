--liquibase formatted sql

--changeset lavarapido:pay-016-payment-images-as-data dbms:mssql
--comment: Mientras no haya almacenamiento de archivos, el QR de la cuenta y el comprobante se guardan como imagen (data URL); 500 caracteres no alcanzan.
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:500 SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'payment' AND TABLE_NAME = 'payment_account' AND COLUMN_NAME = 'qr_image_url'
ALTER TABLE [payment].payment_account ALTER COLUMN qr_image_url NVARCHAR(MAX) NULL;
ALTER TABLE [payment].payment_receipt ALTER COLUMN file_url NVARCHAR(MAX) NOT NULL;
--rollback ALTER TABLE [payment].payment_account ALTER COLUMN qr_image_url NVARCHAR(500) NULL;
