-- list user tables
SELECT name FROM sys.tables ORDER BY name;

-- see sample rows from a table (replace Patients with a real table name you see)
SELECT TOP (50) * FROM dbo.Patients;
