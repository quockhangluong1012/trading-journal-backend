-- =============================================================
-- SQL Server Tables for Setup Feature
-- Schema: Setups
-- Compatible with TradingJournal EntityBase<int> pattern
-- =============================================================

-- Create the schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Setups')
BEGIN
    EXEC('CREATE SCHEMA [Setups]');
END
GO

-- =============================================================
-- 1. TradingSetups - Main setup / trading model table
-- =============================================================
CREATE TABLE [Setups].[TradingSetups]
(
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [Name]          NVARCHAR(200)   NOT NULL,
    [Model]         NVARCHAR(100)   NOT NULL,
    [Description]   NVARCHAR(MAX)   NULL,
    [Status]        INT             NOT NULL DEFAULT 1,   -- 1=Active, 2=Draft, 3=Archived
    [Notes]         NVARCHAR(MAX)   NULL,

    -- Tracking (EntityBase<int>)
    [CreatedDate]   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy]     INT             NOT NULL DEFAULT 0,
    [IsDisabled]    BIT             NOT NULL DEFAULT 0,
    [UpdatedDate]   DATETIME2       NULL,
    [UpdatedBy]     INT             NULL,

    CONSTRAINT [PK_TradingSetups] PRIMARY KEY CLUSTERED ([Id])
);
GO

-- =============================================================
-- 2. SetupSteps - Flowchart nodes / steps for a setup
-- =============================================================
CREATE TABLE [Setups].[SetupSteps]
(
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [TradingSetupId] INT            NOT NULL,
    [StepNumber]    INT             NOT NULL,
    [Label]         NVARCHAR(200)   NOT NULL,
    [Description]   NVARCHAR(MAX)   NULL,
    [NodeType]      NVARCHAR(50)    NOT NULL DEFAULT 'setupNode',
    [Color]         NVARCHAR(20)    NULL DEFAULT '#6366f1',

    -- Position on flowchart canvas
    [PositionX]     FLOAT           NOT NULL DEFAULT 0,
    [PositionY]     FLOAT           NOT NULL DEFAULT 0,

    -- Tracking (EntityBase<int>)
    [CreatedDate]   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy]     INT             NOT NULL DEFAULT 0,
    [IsDisabled]    BIT             NOT NULL DEFAULT 0,
    [UpdatedDate]   DATETIME2       NULL,
    [UpdatedBy]     INT             NULL,

    CONSTRAINT [PK_SetupSteps] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SetupSteps_TradingSetups] FOREIGN KEY ([TradingSetupId])
        REFERENCES [Setups].[TradingSetups]([Id]) ON DELETE CASCADE
);
GO

-- =============================================================
-- 3. SetupConnections - Flowchart edges between steps
-- =============================================================
CREATE TABLE [Setups].[SetupConnections]
(
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [TradingSetupId] INT            NOT NULL,
    [SourceStepId]  INT             NOT NULL,
    [TargetStepId]  INT             NOT NULL,
    [Label]         NVARCHAR(200)   NULL,
    [IsAnimated]    BIT             NOT NULL DEFAULT 1,
    [Color]         NVARCHAR(20)    NULL DEFAULT '#6366f1',

    -- Tracking (EntityBase<int>)
    [CreatedDate]   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy]     INT             NOT NULL DEFAULT 0,
    [IsDisabled]    BIT             NOT NULL DEFAULT 0,
    [UpdatedDate]   DATETIME2       NULL,
    [UpdatedBy]     INT             NULL,

    CONSTRAINT [PK_SetupConnections] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SetupConnections_TradingSetups] FOREIGN KEY ([TradingSetupId])
        REFERENCES [Setups].[TradingSetups]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SetupConnections_SourceStep] FOREIGN KEY ([SourceStepId])
        REFERENCES [Setups].[SetupSteps]([Id]),
    CONSTRAINT [FK_SetupConnections_TargetStep] FOREIGN KEY ([TargetStepId])
        REFERENCES [Setups].[SetupSteps]([Id])
);
GO

-- =============================================================
-- Indexes
-- =============================================================
CREATE NONCLUSTERED INDEX [IX_SetupSteps_TradingSetupId]
    ON [Setups].[SetupSteps]([TradingSetupId]);
GO

CREATE NONCLUSTERED INDEX [IX_SetupConnections_TradingSetupId]
    ON [Setups].[SetupConnections]([TradingSetupId]);
GO

CREATE NONCLUSTERED INDEX [IX_SetupConnections_SourceStepId]
    ON [Setups].[SetupConnections]([SourceStepId]);
GO

CREATE NONCLUSTERED INDEX [IX_SetupConnections_TargetStepId]
    ON [Setups].[SetupConnections]([TargetStepId]);
GO

-- =============================================================
-- Seed Data: ICT 2022 Model Example
-- =============================================================
INSERT INTO [Setups].[TradingSetups] ([Name], [Model], [Description], [Status])
VALUES (
    N'ICT 2022 Model',
    N'ICT',
    N'Inner Circle Trader 2022 mentorship trading model. Focuses on market structure, order blocks, fair value gaps, and liquidity sweeps. Uses smart money concepts to identify institutional order flow.',
    1 -- Active
);

DECLARE @SetupId INT = SCOPE_IDENTITY();

-- Insert Steps
INSERT INTO [Setups].[SetupSteps] ([TradingSetupId], [StepNumber], [Label], [Description], [Color], [PositionX], [PositionY])
VALUES
    (@SetupId, 1,  N'Market Analysis',         N'Analyze the higher timeframe (Monthly, Weekly, Daily) to determine overall market context and trend direction.',                                 '#6366f1', 400,    0),
    (@SetupId, 2,  N'HTF Bias (Daily/4H)',      N'Determine bullish or bearish bias using daily and 4H candle closures, displacement, and market structure shifts.',                               '#8b5cf6', 400,  150),
    (@SetupId, 3,  N'Identify Key Levels',      N'Mark previous day high/low, session highs/lows, order blocks, breakers, and fair value gaps on higher timeframes.',                              '#a855f7', 150,  300),
    (@SetupId, 4,  N'Session Selection',         N'Focus on London (02:00-05:00 EST) or New York (07:00-10:00 EST) killzones for trade execution.',                                                '#a855f7', 650,  300),
    (@SetupId, 5,  N'PD Array / Entry Zone',    N'Wait for price to reach a premium/discount array: order block, FVG, breaker block, or mitigation block.',                                       '#d946ef', 400,  450),
    (@SetupId, 6,  N'Liquidity Sweep',           N'Confirm a liquidity sweep (stop hunt) above/below a key level before looking for entry. This is the Judas Swing.',                              '#ec4899', 150,  600),
    (@SetupId, 7,  N'Market Structure Shift',   N'Wait for a displacement candle causing a break of structure (BOS) or change of character (ChoCH) on LTF (5m/15m).',                             '#ec4899', 650,  600),
    (@SetupId, 8,  N'Entry (OTE / FVG)',         N'Enter at the Optimal Trade Entry (62-79% Fibonacci retracement) or into a fair value gap after the displacement.',                               '#f43f5e', 400,  750),
    (@SetupId, 9,  N'Stop Loss Placement',       N'Place SL beyond the swing high/low that created the displacement. Typically 10-20 pips beyond the structure.',                                   '#ef4444', 150,  900),
    (@SetupId, 10, N'Take Profit Targets',       N'Target the opposite liquidity pool, previous session high/low, or a measured move. Minimum 1:3 RR.',                                            '#10b981', 650,  900),
    (@SetupId, 11, N'Trade Management',          N'Move SL to breakeven after 1:1 RR. Trail stop using LTF market structure. Take partials at key levels.',                                         '#06b6d4', 400, 1050);

-- Get step IDs for connections
DECLARE @Step1 INT, @Step2 INT, @Step3 INT, @Step4 INT, @Step5 INT,
        @Step6 INT, @Step7 INT, @Step8 INT, @Step9 INT, @Step10 INT, @Step11 INT;

SELECT @Step1  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 1;
SELECT @Step2  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 2;
SELECT @Step3  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 3;
SELECT @Step4  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 4;
SELECT @Step5  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 5;
SELECT @Step6  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 6;
SELECT @Step7  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 7;
SELECT @Step8  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 8;
SELECT @Step9  = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 9;
SELECT @Step10 = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 10;
SELECT @Step11 = Id FROM [Setups].[SetupSteps] WHERE [TradingSetupId] = @SetupId AND [StepNumber] = 11;

-- Insert Connections (edges)
INSERT INTO [Setups].[SetupConnections] ([TradingSetupId], [SourceStepId], [TargetStepId], [Color])
VALUES
    (@SetupId, @Step1,  @Step2,  '#6366f1'),   -- Market Analysis -> HTF Bias
    (@SetupId, @Step2,  @Step3,  '#8b5cf6'),   -- HTF Bias -> Identify Key Levels
    (@SetupId, @Step2,  @Step4,  '#8b5cf6'),   -- HTF Bias -> Session Selection
    (@SetupId, @Step3,  @Step5,  '#a855f7'),   -- Key Levels -> PD Array
    (@SetupId, @Step4,  @Step5,  '#a855f7'),   -- Session Selection -> PD Array
    (@SetupId, @Step5,  @Step6,  '#d946ef'),   -- PD Array -> Liquidity Sweep
    (@SetupId, @Step5,  @Step7,  '#d946ef'),   -- PD Array -> Market Structure Shift
    (@SetupId, @Step6,  @Step8,  '#ec4899'),   -- Liquidity Sweep -> Entry
    (@SetupId, @Step7,  @Step8,  '#ec4899'),   -- Market Structure Shift -> Entry
    (@SetupId, @Step8,  @Step9,  '#f43f5e'),   -- Entry -> Stop Loss
    (@SetupId, @Step8,  @Step10, '#f43f5e'),   -- Entry -> Take Profit
    (@SetupId, @Step9,  @Step11, '#ef4444'),   -- Stop Loss -> Trade Management
    (@SetupId, @Step10, @Step11, '#10b981');   -- Take Profit -> Trade Management
GO
