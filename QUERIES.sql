-- QUERIES.sql (SQL Server)
-- Schema: Users, Products, Orders, OrderItems (see Part A)
-- Order.Status is stored as text: 'Pending','Confirmed','Shipped','Cancelled'

-- 1. Top 5 customers by total spend (Confirmed + Shipped) in the last 30 days
SELECT TOP (5)
    u.Id,
    u.FullName,
    u.Email,
    SUM(o.Total) AS TotalSpend,
    COUNT(*)     AS OrderCount
FROM Orders o
JOIN Users u ON u.Id = o.CustomerId
WHERE o.Status IN ('Confirmed', 'Shipped')
  AND o.CreatedAt >= DATEADD(DAY, -30, SYSUTCDATETIME())
GROUP BY u.Id, u.FullName, u.Email
ORDER BY TotalSpend DESC;

-- 2. Products that have never been ordered
SELECT p.Id, p.Sku, p.Name
FROM Products p
WHERE NOT EXISTS (SELECT 1 FROM OrderItems oi WHERE oi.ProductId = p.Id);

-- 3. Monthly revenue for the current year, with a running total
--    (revenue = Confirmed + Shipped orders)
SELECT
    MONTH(o.CreatedAt)                        AS [Month],
    SUM(o.Total)                              AS MonthlyRevenue,
    SUM(SUM(o.Total)) OVER (ORDER BY MONTH(o.CreatedAt)
                            ROWS UNBOUNDED PRECEDING) AS RunningTotal
FROM Orders o
WHERE o.Status IN ('Confirmed', 'Shipped')
  AND YEAR(o.CreatedAt) = YEAR(SYSUTCDATETIME())
GROUP BY MONTH(o.CreatedAt)
ORDER BY [Month];

-- 4. Each customer's second-highest order total
--    (customers with fewer than 2 orders do not appear)
--    ROW_NUMBER: if two orders have the same total, the second one still counts as "second order".
SELECT CustomerId, Total AS SecondHighestOrderTotal
FROM (
    SELECT o.CustomerId,
           o.Total,
           ROW_NUMBER() OVER (PARTITION BY o.CustomerId ORDER BY o.Total DESC, o.Id) AS rn
    FROM Orders o
) x
WHERE rn = 2;

-- 5. Products whose stock is below the average quantity sold per order of that product
SELECT p.Id, p.Sku, p.Name, p.StockQuantity, a.AvgQtyPerOrder
FROM Products p
JOIN (
    SELECT oi.ProductId,
           AVG(CAST(oi.Quantity AS DECIMAL(18, 2))) AS AvgQtyPerOrder
    FROM OrderItems oi
    GROUP BY oi.ProductId
) a ON a.ProductId = p.Id
WHERE p.StockQuantity < a.AvgQtyPerOrder;