namespace RzR.DataVigil.EFCore.Tests.Helpers
{
    internal sealed class RefRow
    {
        public RefRow(int id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public int Id { get; }

        public string Name { get; }

        public string Description { get; }
    }
}
