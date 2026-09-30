--liquibase formatted sql

--changeset merari:pay-012-payment-receipt dbms:mssql
--comment: Proof of payment uploaded by the customer. uploaded_by/reviewed_by -> security.app_user (no FK, cross-service)
CREATE TABLE [payment].payment_receipt (
    payment_receipt_id    BIGINT        IDENTITY(1,1) NOT NULL,
    payment_id            BIGINT        NOT NULL,
    file_url              NVARCHAR(500) NOT NULL,
    transaction_reference NVARCHAR(100) NULL,
    reported_amount       DECIMAL(12,2) NULL,
    uploaded_at           DATETIME2(3)  NOT NULL CONSTRAINT df_preceipt_uploaded DEFAULT SYSUTCDATETIME(),
    uploaded_by           BIGINT        NOT NULL,
    reviewed_at           DATETIME2(3)  NULL,
    reviewed_by           BIGINT        NULL,
    review_comment        NVARCHAR(300) NULL,
    created_at            DATETIME2(3)  NOT NULL CONSTRAINT df_preceipt_created DEFAULT SYSUTCDATETIME(),
    created_by            BIGINT        NULL,
    updated_at            DATETIME2(3)  NULL,
    updated_by            BIGINT        NULL,
    deleted_at            DATETIME2(3)  NULL,
    deleted_by            BIGINT        NULL,
    row_version           INT           NOT NULL CONSTRAINT df_preceipt_rv DEFAULT 1,
    CONSTRAINT pk_payment_receipt PRIMARY KEY (payment_receipt_id),
    CONSTRAINT fk_payment_receipt_payment FOREIGN KEY (payment_id) REFERENCES [payment].payment(payment_id),
    CONSTRAINT ck_preceipt_amount CHECK (reported_amount IS NULL OR reported_amount > 0),
    CONSTRAINT ck_preceipt_review CHECK (
        (reviewed_at IS NULL AND reviewed_by IS NULL) OR
        (reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL)
    )
);

CREATE INDEX ix_payment_receipt_payment     ON [payment].payment_receipt (payment_id);
CREATE INDEX ix_payment_receipt_reference   ON [payment].payment_receipt (transaction_reference);
CREATE INDEX ix_payment_receipt_reviewed_by ON [payment].payment_receipt (reviewed_by);
CREATE INDEX ix_payment_receipt_uploaded_by ON [payment].payment_receipt (uploaded_by);
--rollback DROP TABLE [payment].payment_receipt;
