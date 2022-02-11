using System;
using Dapper.Contrib.Extensions;

namespace CorpayOne.MysqlTestDummy.Tests.Entities
{
    [Table("Products")]
    internal class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime Created { get; set; }

        public string ImageUrl { get; set; }

        // ReSharper disable once InconsistentNaming
        public string SKU { get; set; }

        public string Subtitle { get; set; }
    }

    [Table("Users")]
    internal class User
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string Country { get; set; }
    }

    [Table("Orders")]
    internal class Order
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }
    }
}
