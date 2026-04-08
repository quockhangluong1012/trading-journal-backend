-- Migration: Add ChecklistModels table and FK on PretradeChecklists
-- Run this script against the TradingJournal database

-- 1. Create the ChecklistModels table
IF NOT EXISTS (SELECT * FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'Trades' AND t.name = 'ChecklistModels')
BEGIN
    CREATE TABLE [Trades].[ChecklistModels] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [Name]        NVARCHAR(256)  NOT NULL,
        [Description] NVARCHAR(1024) NULL,
        [CreatedDate] DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]   INT            NOT NULL DEFAULT 0,
        [UpdatedDate] DATETIME2      NULL,
        [UpdatedBy]   INT            NULL,
        CONSTRAINT [PK_ChecklistModels] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- 2. Seed default "ICT 2022" model
IF NOT EXISTS (SELECT 1 FROM [Trades].[ChecklistModels] WHERE [Name] = 'ICT 2022')
BEGIN
    INSERT INTO [Trades].[ChecklistModels] ([Name], [Description], [CreatedDate], [CreatedBy])
    VALUES ('ICT 2022', 'Inner Circle Trader 2022 Model - Market structure, trading setup, risk management, and psychology checklist.', GETUTCDATE(), 0);
END
GO

-- 3. Add ChecklistModelId column to PretradeChecklists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Trades.PretradeChecklists') AND name = 'ChecklistModelId')
BEGIN
    -- Add column as nullable first
    ALTER TABLE [Trades].[PretradeChecklists]
        ADD [ChecklistModelId] INT NULL;

    -- Assign all existing rows to the default "ICT 2022" model
    UPDATE [Trades].[PretradeChecklists]
    SET [ChecklistModelId] = (SELECT TOP 1 [Id] FROM [Trades].[ChecklistModels] WHERE [Name] = 'ICT 2022')
    WHERE [ChecklistModelId] IS NULL;

    -- Make column NOT NULL
    ALTER TABLE [Trades].[PretradeChecklists]
        ALTER COLUMN [ChecklistModelId] INT NOT NULL;

    -- Add foreign key constraint
    ALTER TABLE [Trades].[PretradeChecklists]
        ADD CONSTRAINT [FK_PretradeChecklists_ChecklistModels]
        FOREIGN KEY ([ChecklistModelId]) REFERENCES [Trades].[ChecklistModels]([Id]);
END
GO
