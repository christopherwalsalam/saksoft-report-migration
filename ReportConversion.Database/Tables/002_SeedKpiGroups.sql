-- ============================================================
-- Seed KPI Groups
-- ============================================================
INSERT INTO KpiGroups (Name, Description, CreatedAt)
SELECT Name, Description, GETUTCDATE()
FROM (VALUES
    ('Financial Performance',       'Reports measuring financial metrics, P&L, budgets, and forecasts'),
    ('Sales & Revenue',             'Reports tracking sales performance, revenue, and growth'),
    ('Operations & Logistics',      'Reports on operational efficiency, supply chain, and logistics'),
    ('HR & Workforce',              'Reports on headcount, payroll, performance, and HR metrics'),
    ('Customer Analytics',          'Reports on customer behaviour, satisfaction, and retention'),
    ('Inventory & Supply Chain',    'Reports on stock levels, procurement, and supplier performance'),
    ('Compliance & Audit',          'Reports for regulatory compliance, audits, and risk management'),
    ('Other / Unclassified',        'Reports not yet classified into a specific KPI domain')
) AS src(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM KpiGroups WHERE KpiGroups.Name = src.Name);
GO
