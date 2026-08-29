namespace RzR.DataVigil.EFCore.Tests.Helpers
{
    internal sealed class EnumMember
    {
        public EnumMember(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public int Value { get; }
    }
}
