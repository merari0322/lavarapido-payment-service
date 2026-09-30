--liquibase formatted sql

--changeset merari:pay-002-create-schema-payment dbms:mssql
--comment: Schema owned by payment-service
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM sys.schemas WHERE name = 'payment'
EXEC('CREATE SCHEMA [payment]');
--rollback EXEC('DROP SCHEMA [payment]');
