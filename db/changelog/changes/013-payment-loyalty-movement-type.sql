--liquibase formatted sql

--changeset merari:pay-013-loyalty-movement-type dbms:mssql
--comment: Kinds of loyalty point movement (moved from customer to payment, ADR-009)
CREATE TABLE [payment].loyalty_movement_type (
    loyalty_movement_type_id SMALLINT     IDENTITY(1,1) NOT NULL,
    code          NVARCHAR(30) NOT NULL,
    name          NVARCHAR(60) NOT NULL,
    sign          SMALLINT     NOT NULL,
    display_order SMALLINT     NOT NULL CONSTRAINT df_lmt_order DEFAULT 0,
    is_active     BIT          NOT NULL CONSTRAINT df_lmt_active DEFAULT 1,
    created_at    DATETIME2(3) NOT NULL CONSTRAINT df_lmt_created DEFAULT SYSUTCDATETIME(),
    created_by    BIGINT       NULL,
    updated_at    DATETIME2(3) NULL,
    updated_by    BIGINT       NULL,
    deleted_at    DATETIME2(3) NULL,
    deleted_by    BIGINT       NULL,
    row_version   INT          NOT NULL CONSTRAINT df_lmt_rv DEFAULT 1,
    CONSTRAINT pk_loyalty_movement_type PRIMARY KEY (loyalty_movement_type_id),
    CONSTRAINT uq_loyalty_movement_type_code UNIQUE (code),
    CONSTRAINT ck_loyalty_movement_sign CHECK (sign IN (-1, 1))
);
--rollback DROP TABLE [payment].loyalty_movement_type;
