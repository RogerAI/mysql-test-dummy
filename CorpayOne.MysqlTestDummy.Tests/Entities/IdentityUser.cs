using System;
using Dapper.Contrib.Extensions;

namespace CorpayOne.MysqlTestDummy.Tests.Entities
{
    [Table("IdentityUsers")]
    internal class IdentityUser
    {
        [Key]
        public Guid UserId { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }
    }

    [Table("IdentityRoles")]
    internal class IdentityRole
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }

    [Table("IdentityUserRoles")]
    internal class IdentityUserRole
    {
        public Guid UserId { get; set; }

        public Guid RoleId { get; set; }
    }
}
