--liquibase formatted sql

--changeset merari:pay-001-create-schema-promotion dbms:mssql
--comment: Schema owned by payment-service
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM sys.schemas WHERE name = 'promotion'
EXEC('CREATE SCHEMA [promotion]');
--rollback EXEC('DROP SCHEMA [promotion]');
