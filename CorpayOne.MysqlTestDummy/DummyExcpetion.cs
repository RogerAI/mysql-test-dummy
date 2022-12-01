namespace CorpayOne.MysqlTestDummy;

public class DummyException : Exception
{
    public DummyException(string error) : base(error)
    { }
}