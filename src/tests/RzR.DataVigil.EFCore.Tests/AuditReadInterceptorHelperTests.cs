using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.EFCore.Helpers;
using RzR.DataVigil.EFCore.Tests.Stubs;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AuditReadInterceptorHelperTests
    {
        [TestMethod]
        public void ParseTableNames_DoubleQuoted_SchemaAndTable()
        {
            var sql = "SELECT \"t\".\"Id\" FROM \"blog\".\"Posts\" AS \"t\"";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("blog", tables[0].Schema);
            Assert.AreEqual("Posts", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_BracketQuoted_SchemaAndTable()
        {
            var sql = "SELECT [t].[Id] FROM [dbo].[Orders] AS [t]";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("dbo", tables[0].Schema);
            Assert.AreEqual("Orders", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_Unquoted_SchemaAndTable()
        {
            var sql = "SELECT t.Id FROM dbo.Orders AS t";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("dbo", tables[0].Schema);
            Assert.AreEqual("Orders", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_TableOnly_NoSchema()
        {
            var sql = "SELECT t.Id FROM Posts AS t";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.IsNull(tables[0].Schema);
            Assert.AreEqual("Posts", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_DoubleQuoted_TableOnly()
        {
            var sql = "SELECT \"t\".\"Name\" FROM \"Products\" AS \"t\"";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.IsNull(tables[0].Schema);
            Assert.AreEqual("Products", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_BracketQuoted_TableOnly()
        {
            var sql = "SELECT [t].[Name] FROM [Products] AS [t]";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.IsNull(tables[0].Schema);
            Assert.AreEqual("Products", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_JoinClause_ExtractsBothTables()
        {
            var sql = "SELECT \"t\".\"Id\", \"c\".\"Name\" FROM \"blog\".\"Posts\" AS \"t\" INNER JOIN \"blog\".\"Comments\" AS \"c\" ON \"t\".\"Id\" = \"c\".\"PostId\"";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(2, tables.Count);
            Assert.AreEqual("Posts", tables[0].Table);
            Assert.AreEqual("Comments", tables[1].Table);
        }

        [TestMethod]
        public void ParseTableNames_MultipleJoins_AllExtracted()
        {
            var sql = "SELECT t.Id FROM dbo.Orders AS t " +
                      "LEFT JOIN dbo.OrderItems AS oi ON t.Id = oi.OrderId " +
                      "INNER JOIN dbo.Products AS p ON oi.ProductId = p.Id";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(3, tables.Count);
            Assert.AreEqual("Orders", tables[0].Table);
            Assert.AreEqual("OrderItems", tables[1].Table);
            Assert.AreEqual("Products", tables[2].Table);
        }

        [TestMethod]
        public void ParseTableNames_DuplicateTable_DeduplicatedByCaseInsensitiveKey()
        {
            var sql = "SELECT 1 FROM dbo.Orders AS t INNER JOIN dbo.Orders AS t2 ON t.Id = t2.ParentId";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("Orders", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_CaseInsensitiveKeywords()
        {
            var sql = "select t.Id from dbo.Orders as t inner join dbo.Items as i on t.Id = i.OrderId";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(2, tables.Count);
            Assert.AreEqual("Orders", tables[0].Table);
            Assert.AreEqual("Items", tables[1].Table);
        }

        [TestMethod]
        public void ParseTableNames_MixedQuoteStyles()
        {
            var sql = "SELECT 1 FROM \"public\".\"Posts\" AS t INNER JOIN [dbo].[Comments] AS c ON t.Id = c.PostId";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(2, tables.Count);
            Assert.AreEqual("Posts", tables[0].Table);
            Assert.AreEqual("Comments", tables[1].Table);
        }

        [TestMethod]
        public void ParseTableNames_TrailingSemicolon()
        {
            var sql = "SELECT 1 FROM Orders;";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("Orders", tables[0].Table);
        }

        [TestMethod]
        public void ParseTableNames_EmptySql_ReturnsEmpty()
        {
            var tables = AuditReadInterceptorHelper.ParseTableNames("");
            Assert.AreEqual(0, tables.Count);
        }

        [TestMethod]
        public void ParseTableNames_NoFromClause_ReturnsEmpty()
        {
            var sql = "INSERT INTO Orders (Id) VALUES (1)";
            var tables = AuditReadInterceptorHelper.ParseTableNames(sql);

            Assert.AreEqual(0, tables.Count);
        }

        
        [TestMethod]
        public void ParseSelectedColumns_DoubleQuoted_ExtractsColumns()
        {
            var sql = "SELECT \"t\".\"Id\", \"t\".\"CustomerName\", \"t\".\"Total\" FROM \"dbo\".\"Orders\" AS \"t\"";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(3, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsTrue(cols.Contains("CustomerName"));
            Assert.IsTrue(cols.Contains("Total"));
        }

        [TestMethod]
        public void ParseSelectedColumns_BracketQuoted_ExtractsColumns()
        {
            var sql = "SELECT [t].[Id], [t].[Name], [t].[Price] FROM [dbo].[Products] AS [t]";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(3, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsTrue(cols.Contains("Name"));
            Assert.IsTrue(cols.Contains("Price"));
        }

        [TestMethod]
        public void ParseSelectedColumns_Unquoted_ExtractsColumns()
        {
            var sql = "SELECT t.Id, t.Status, t.CreatedAt FROM dbo.Orders AS t";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(3, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsTrue(cols.Contains("Status"));
            Assert.IsTrue(cols.Contains("CreatedAt"));
        }

        [TestMethod]
        public void ParseSelectedColumns_CaseInsensitiveLookup()
        {
            var sql = "SELECT \"t\".\"CustomerName\" FROM \"Orders\"";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.IsTrue(cols.Contains("customername"));
            Assert.IsTrue(cols.Contains("CUSTOMERNAME"));
        }

        [TestMethod]
        public void ParseSelectedColumns_DuplicateColumnNames_Deduplicated()
        {
            var sql = "SELECT \"t\".\"Id\", \"t\".\"Id\" FROM \"Orders\"";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(1, cols.Count);
        }

        [TestMethod]
        public void ParseSelectedColumns_ColumnsFromMultipleAliases()
        {
            var sql = "SELECT \"t\".\"Id\", \"c\".\"Content\" FROM \"Posts\" AS \"t\" INNER JOIN \"Comments\" AS \"c\" ON \"t\".\"Id\" = \"c\".\"PostId\"";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            // Only columns before FROM are selected columns
            Assert.IsNotNull(cols);
            Assert.AreEqual(2, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsTrue(cols.Contains("Content"));
        }

        [TestMethod]
        public void ParseSelectedColumns_NoFromClause_ReturnsNull()
        {
            var sql = "SELECT 1";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNull(cols);
        }

        [TestMethod]
        public void ParseSelectedColumns_SelectStar_ReturnsNull()
        {
            var sql = "SELECT * FROM Orders";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            // No alias.column patterns — returns null
            Assert.IsNull(cols);
        }

        [TestMethod]
        public void ParseSelectedColumns_MixedQuoteStyles()
        {
            var sql = "SELECT \"t\".\"Id\", [t].[Name], t.Status FROM Orders AS t";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(3, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsTrue(cols.Contains("Name"));
            Assert.IsTrue(cols.Contains("Status"));
        }

        [TestMethod]
        public void ParseSelectedColumns_IgnoresColumnsAfterFrom()
        {
            var sql = "SELECT \"t\".\"Id\" FROM \"Orders\" AS \"t\" WHERE \"t\".\"Status\" = @p0";
            var cols = AuditReadInterceptorHelper.ParseSelectedColumns(sql);

            Assert.IsNotNull(cols);
            Assert.AreEqual(1, cols.Count);
            Assert.IsTrue(cols.Contains("Id"));
            Assert.IsFalse(cols.Contains("Status"));
        }

        
        [TestMethod]
        public void ExtractEntityId_DoubleQuoted_WhereClause()
        {
            var sql = "SELECT \"t\".\"Id\" FROM \"Orders\" AS \"t\" WHERE \"t\".\"Id\" = @__id_0";
            var parameters = CreateParameters(("@__id_0", "42"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual("42", id);
        }

        [TestMethod]
        public void ExtractEntityId_BracketQuoted_WhereClause()
        {
            var sql = "SELECT [t].[Id] FROM [dbo].[Orders] AS [t] WHERE [t].[Id] = @p0";
            var parameters = CreateParameters(("@p0", "abc-123"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual("abc-123", id);
        }

        [TestMethod]
        public void ExtractEntityId_Unquoted_WhereClause()
        {
            var sql = "SELECT t.Id FROM Orders AS t WHERE t.Id = @p0";
            var parameters = CreateParameters(("@p0", "99"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual("99", id);
        }

        [TestMethod]
        public void ExtractEntityId_NoWhereClause_ReturnsNull()
        {
            var sql = "SELECT \"t\".\"Id\" FROM \"Orders\" AS \"t\"";
            var parameters = CreateParameters();

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.IsNull(id);
        }

        [TestMethod]
        public void ExtractEntityId_WhereOnNonIdColumn_ReturnsNull()
        {
            var sql = "SELECT \"t\".\"Id\" FROM \"Orders\" AS \"t\" WHERE \"t\".\"Status\" = @p0";
            var parameters = CreateParameters(("@p0", "Active"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.IsNull(id);
        }

        [TestMethod]
        public void ExtractEntityId_ParameterNotFound_ReturnsNull()
        {
            var sql = "SELECT t.Id FROM Orders AS t WHERE t.Id = @__id_0";
            var parameters = CreateParameters(("@other_param", "42"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.IsNull(id);
        }

        [TestMethod]
        public void ExtractEntityId_GuidParameter()
        {
            var guid = "a1b2c3d4-e5f6-4789-9abc-def012345678";
            var sql = "SELECT [t].[Id] FROM [Orders] AS [t] WHERE [t].[Id] = @__id_0";
            var parameters = CreateParameters(("@__id_0", guid));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual(guid, id);
        }

        [TestMethod]
        public void ExtractEntityId_CaseInsensitiveWhere()
        {
            var sql = "select t.Id from Orders as t where t.Id = @p0";
            var parameters = CreateParameters(("@p0", "7"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual("7", id);
        }

        [TestMethod]
        public void ExtractEntityId_CaseInsensitiveParameterMatch()
        {
            var sql = "SELECT t.Id FROM Orders AS t WHERE t.Id = @P0";
            var parameters = CreateParameters(("@p0", "55"));

            var id = AuditReadInterceptorHelper.ExtractEntityId(sql, parameters);
            Assert.AreEqual("55", id);
        }

        private static StubParameterCollection CreateParameters(params (string name, string value)[] items)
        {
            var collection = new StubParameterCollection();
            foreach (var (name, value) in items)
                collection.Add(new StubDbParameter { ParameterName = name, Value = value });
            return collection;
        }
    }
}
