USE [FlashAssessment];
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.SensitiveWord)
BEGIN
    INSERT INTO dbo.SensitiveWord (Word, NormalizedWord, Category, Severity, IsActive)
    VALUES
    (N'Create', N'create', N'General', 2, 1),
    (N'String', N'string', N'General', 1, 1),
    (N'Password', N'password', N'Credential', 5, 1),
    (N'SSN', N'ssn', N'PII', 5, 1),
    (N'CreditCard', N'creditcard', N'PII', 5, 1),
    (N'Email', N'email', N'PII', 3, 1),
    (N'Phone', N'phone', N'PII', 3, 1),
    (N'Address', N'address', N'PII', 2, 1),
    (N'Secret', N'secret', N'Credential', 4, 1),
    (N'Token', N'token', N'Credential', 4, 1);

    INSERT INTO dbo.SanitizationRule (WordId, WholeWordOnly, CaseSensitive, AllowInsideCompound, IsActive)
    SELECT TOP 3 SensitiveWordId, 1, 0, 0, 1 FROM dbo.SensitiveWord ORDER BY SensitiveWordId;
END
GO

