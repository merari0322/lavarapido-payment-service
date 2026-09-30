--liquibase formatted sql

--changeset merari:pay-008-payment-method-type dbms:mssql
--comment: Cash, Nequi, Daviplata, bank transfer
CREATE TABLE [payment].payment_method_type (
    payment_method_type_id SMALLINT     IDENTITY(1,1) NOT NULL,
    code             NVARCHAR(30) NOT NULL,
    name             NVARCHAR(60) NOT NULL,
    requires_account BIT          NOT NULL CONSTRAINT df_pmt_account DEFAULT 1,
    requires_receipt BIT          NOT NULL CONSTRAINT df_pmt_receipt DEFAULT 1,
    display_order    SMALLINT     NOT NULL CONSTRAINT df_pmt_order DEFAULT 0,
    is_active        BIT          NOT NULL CONSTRAINT df_pmt_active DEFAULT 1,
    created_at       DATETIME2(3) NOT NULL CONSTRAINT df_pmt_created DEFAULT SYSUTCDATETIME(),
    created_by       BIGINT       NULL,
    updated_at       DATETIME2(3) NULL,
    updated_by       BIGINT       NULL,
    deleted_at       DATETIME2(3) NULL,
    deleted_by       BIGINT       NULL,
    row_version      INT          NOT NULL CONSTRAINT df_pmt_rv DEFAULT 1,
    CONSTRAINT pk_payment_method_type PRIMARY KEY (payment_method_type_id),
    CONSTRAINT uq_payment_method_type_code UNIQUE (code)
);
--rollback DROP TABLE [payment].payment_method_type;
