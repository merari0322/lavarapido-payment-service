--liquibase formatted sql

--changeset merari:pay-003-discount-type dbms:mssql
--comment: Percentage or fixed amount
CREATE TABLE [promotion].discount_type (
    discount_type_id SMALLINT     IDENTITY(1,1) NOT NULL,
    code          NVARCHAR(30) NOT NULL,
    name          NVARCHAR(60) NOT NULL,
    display_order SMALLINT     NOT NULL CONSTRAINT df_dtype_order DEFAULT 0,
    is_active     BIT          NOT NULL CONSTRAINT df_dtype_active DEFAULT 1,
    created_at    DATETIME2(3) NOT NULL CONSTRAINT df_dtype_created DEFAULT SYSUTCDATETIME(),
    created_by    BIGINT       NULL,
    updated_at    DATETIME2(3) NULL,
    updated_by    BIGINT       NULL,
    deleted_at    DATETIME2(3) NULL,
    deleted_by    BIGINT       NULL,
    row_version   INT          NOT NULL CONSTRAINT df_dtype_rv DEFAULT 1,
    CONSTRAINT pk_discount_type PRIMARY KEY (discount_type_id),
    CONSTRAINT uq_discount_type_code UNIQUE (code)
);
--rollback DROP TABLE [promotion].discount_type;
