using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class StubDbParameter : DbParameter
    {
        public override string ParameterName { get; set; }
        public override object Value { get; set; }
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string SourceColumn { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }

        public override void ResetDbType() { }
    }

    internal class StubParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _params = new List<DbParameter>();

        public override int Count => _params.Count;
        public override object SyncRoot => ((ICollection)_params).SyncRoot;

        public override int Add(object value)
        {
            _params.Add((DbParameter)value);

            return _params.Count - 1;
        }

        public override void Clear() => _params.Clear();
        public override bool Contains(object value) => _params.Contains((DbParameter)value);
        public override bool Contains(string value) => _params.Exists(p => p.ParameterName == value);
        public override int IndexOf(object value) => _params.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _params.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _params.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _params.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _params.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _params.RemoveAt(IndexOf(parameterName));
        public override void CopyTo(Array array, int index) => ((ICollection)_params).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _params.GetEnumerator();
        public override void AddRange(Array values) { foreach (var v in values) Add(v); }
        protected override DbParameter GetParameter(int index) => _params[index];
        protected override DbParameter GetParameter(string parameterName) => _params[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _params[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _params[IndexOf(parameterName)] = value;
    }
}
