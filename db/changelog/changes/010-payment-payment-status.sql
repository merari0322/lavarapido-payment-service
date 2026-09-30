--liquibase formatted sql

--changeset merari:pay-010-payment-status dbms:mssql
--comment: The five payment states. ID 3 (APPROVED) is hard-coded in ux_payment_one_approved_per_booking - never re-seed in another order.
CREATE TABLE [payment].payment_status (
    payment_status_id SMALLINT     IDENTITY(1,1) NOT NULL,
    code          NVARCHAR(30) NOT NULL,
    name          NVARCHAR(60) NOT NULL,
    is_final      BIT          NOT NULL CONSTRAINT df_pstatus_final DEFAULT 0,
    display_order SMALLINT     NOT NULL CONSTRAINT df_pstatus_order DEFAULT 0,
    is_active     BIT          NOT NULL CONSTRAINT df_pstatus_active DEFAULT 1,
    created_at    DATETIME2(3) NOT NULL CONSTRAINT df_pstatus_created DEFAULT SYSUTCDATETIME(),
    created_by    BIGINT       NULL,
    updated_at    DATETIME2(3) NULL,
    updated_by    BIGINT       NULL,
    deleted_at    DATETIME2(3) NULL,
    deleted_by    BIGINT       NULL,
    row_version   INT          NOT NULL CONSTRAINT df_pstatus_rv DEFAULT 1,
    CONSTRAINT pk_payment_status PRIMARY KEY (payment_status_id),
    CONSTRAINT uq_payment_status_code UNIQUE (code)
);
--rollback DROP TABLE [payment].payment_status;
