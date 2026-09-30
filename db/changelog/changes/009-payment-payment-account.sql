--liquibase formatted sql

--changeset merari:pay-009-payment-account dbms:mssql
--comment: One row per payment method; holds the shop QR image
CREATE TABLE [payment].payment_account (
    payment_account_id     SMALLINT      IDENTITY(1,1) NOT NULL,
    payment_method_type_id SMALLINT      NOT NULL,
    account_holder         NVARCHAR(120) NOT NULL,
    account_number         NVARCHAR(50)  NULL,
    qr_image_url           NVARCHAR(500) NULL,
    instructions           NVARCHAR(300) NULL,
    is_active              BIT           NOT NULL CONSTRAINT df_pacct_active DEFAULT 1,
    created_at             DATETIME2(3)  NOT NULL CONSTRAINT df_pacct_created DEFAULT SYSUTCDATETIME(),
    created_by             BIGINT        NULL,
    updated_at             DATETIME2(3)  NULL,
    updated_by             BIGINT        NULL,
    deleted_at             DATETIME2(3)  NULL,
    deleted_by             BIGINT        NULL,
    row_version            INT           NOT NULL CONSTRAINT df_pacct_rv DEFAULT 1,
    CONSTRAINT pk_payment_account PRIMARY KEY (payment_account_id),
    CONSTRAINT fk_payment_account_method FOREIGN KEY (payment_method_type_id) REFERENCES [payment].payment_method_type(payment_method_type_id)
);
--rollback DROP TABLE [payment].payment_account;
