using Dapper.Contrib.Extensions;

namespace CorpayOne.MysqlTestDummy.Tests.Entities
{
    [Table("Node")]
    internal class Node
    {
        public long Id { get; set; }

        public bool Value { get; set; }

        public long? ParentId { get; set; }
    }
}
