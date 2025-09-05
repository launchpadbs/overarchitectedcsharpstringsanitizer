IF DB_ID('FlashAssessment') IS NULL
BEGIN
    PRINT 'Creating database FlashAssessment';
    CREATE DATABASE [FlashAssessment];
END
GO


