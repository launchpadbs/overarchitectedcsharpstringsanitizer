USE [FlashAssessment];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dbo') EXEC('CREATE SCHEMA dbo');
GO

-- Drop in dependency order: child tables first
IF OBJECT_ID('dbo.SanitizationRule', 'U') IS NOT NULL DROP TABLE dbo.SanitizationRule;
IF OBJECT_ID('dbo.Idempotency', 'U') IS NOT NULL DROP TABLE dbo.Idempotency;
IF OBJECT_ID('dbo.SensitiveWord', 'U') IS NOT NULL DROP TABLE dbo.SensitiveWord;
GO

CREATE TABLE dbo.SensitiveWord
(
	SensitiveWordId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SensitiveWord PRIMARY KEY,
	Word NVARCHAR(128) NOT NULL,
	NormalizedWord NVARCHAR(128) NOT NULL,
	Category NVARCHAR(64) NULL,
	Severity TINYINT NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_SensitiveWord_IsActive DEFAULT(1),
	CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_SensitiveWord_CreatedUtc DEFAULT (SYSUTCDATETIME()),
	RowVersion ROWVERSION NOT NULL
);
GO

CREATE UNIQUE INDEX UX_SensitiveWord_NormalizedWord ON dbo.SensitiveWord (NormalizedWord) WHERE IsActive = 1;
GO

CREATE TABLE dbo.SanitizationRule
(
	SanitizationRuleId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SanitizationRule PRIMARY KEY,
	WordId BIGINT NOT NULL CONSTRAINT FK_SanitizationRule_SensitiveWord REFERENCES dbo.SensitiveWord(SensitiveWordId),
	WholeWordOnly BIT NOT NULL CONSTRAINT DF_SanitizationRule_WholeWordOnly DEFAULT(1),
	CaseSensitive BIT NOT NULL CONSTRAINT DF_SanitizationRule_CaseSensitive DEFAULT(0),
	AllowInsideCompound BIT NOT NULL CONSTRAINT DF_SanitizationRule_AllowInsideCompound DEFAULT(0),
	IsActive BIT NOT NULL CONSTRAINT DF_SanitizationRule_IsActive DEFAULT(1),
	RowVersion ROWVERSION NOT NULL
);
GO

CREATE TABLE dbo.Idempotency
(
	IdempotencyKey NVARCHAR(128) NOT NULL CONSTRAINT PK_Idempotency PRIMARY KEY,
	CreatedUtc DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
	RequestHash VARBINARY(32) NULL,
	ResponsePayload NVARCHAR(MAX) NULL
);
GO

CREATE INDEX IX_SanitizationRule_WordId_IsActive ON dbo.SanitizationRule (WordId, IsActive);
CREATE INDEX IX_SensitiveWord_IsActive ON dbo.SensitiveWord (IsActive);
GO

