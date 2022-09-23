using System;
using System.Data;
using Dapper;

namespace CorpayOne.MysqlTestDummy.Tests
{
    public class GuidStringMapperHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid guid)
        {
            parameter.Value = guid.ToString("D");
        }

        public override Guid Parse(object value)
        {
            var val = (string)value;

            return Guid.Parse(val);
        }
    }
}