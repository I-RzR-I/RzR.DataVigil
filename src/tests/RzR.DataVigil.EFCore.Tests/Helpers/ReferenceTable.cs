using System;

namespace RzR.DataVigil.EFCore.Tests.Helpers
{
    internal sealed class ReferenceTable
    {
        public ReferenceTable(string tableName, Type enumType, Type entityType)
        {
            TableName = tableName;
            EnumType = enumType;
            EntityType = entityType;
        }

        public string TableName { get; }

        public Type EnumType { get; }

        public Type EntityType { get; }
    }
}
