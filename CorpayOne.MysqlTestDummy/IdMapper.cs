using System.Data;

namespace CorpayOne.MysqlTestDummy
{
    internal static class IdMapper
    {
        public static bool TryReadOptional<TId>(IDataRecord reader, out TId? id)
        {
            id = default;

            if (typeof(TId) == typeof(int))
            {
                if (TryReadSimpleType(0, reader, typeof(TId), out var obj) && obj is TId t)
                {
                    id = t;
                    return true;
                }
            }

            if (IsTupleType<TId>())
            {
                var generic = typeof(TId);
                var fields = generic.GenericTypeArguments;

                var instance = (object) Activator.CreateInstance<TId>()!;
                for (var i = 0; i < fields.Length; i++)
                {
                    var fieldType = fields[i];
                    if (!TryReadSimpleType(i, reader, fieldType, out var fieldVal))
                    {
                        return false;
                    }

                    var f = generic.GetField($"Item{i + 1}");
                    f.SetValue(instance, fieldVal);
                }

                id = (TId?)instance;

                return true;
            }

            if (TryReadSimpleType(0, reader, typeof(TId), out var objResult) && objResult is TId tResult)
            {
                id = tResult;
                return true;
            }

            return false;
        }

        private static bool TryReadSimpleType(int index, IDataRecord reader, Type idType, out object? id)
        {
            id = default;

            object? value = null;
            if (!reader.IsDBNull(index))
            {
                value = reader.GetValue(index);
            }

            if (value == null)
            {
                return false;
            }

            if (idType == typeof(int))
            {
                switch (value)
                {
                    case int i:
                        id = i;
                        break;
                    case long l:
                        id = (int)l;
                        break;
                    case uint u:
                        id = (int)u;
                        break;
                    case ulong ul:
                        id = (int)ul;
                        break;
                    case short sh:
                        id = (int)sh;
                        break;
                    case ushort us:
                        id = (int)us;
                        break;
                    default:
                        return false;
                }

                return true;
            }

            if (idType == typeof(long))
            {
                switch (value)
                {
                    case int i:
                        id = (long)i;
                        break;
                    case long l:
                        id = l;
                        break;
                    case uint u:
                        id = (long)u;
                        break;
                    case ulong ul:
                        id = (long)ul;
                        break;
                    case short sh:
                        id = (long)sh;
                        break;
                    case ushort us:
                        id = (long)us;
                        break;
                    default:
                        return false;
                }

                return true;
            }

            if (idType == typeof(string) && value is string s)
            {
                id = s;
                return !string.IsNullOrWhiteSpace(s);
            }

            if (idType == typeof(Guid) && value is byte[] { Length: 16 } b)
            {
                id = new Guid(b);
                return true;
            }

            return false;
        }

        private static bool IsTupleType<TId>()
        {
            var type = typeof(TId);

            if (!type.IsGenericType)
            {
                return false;
            }

            var genericType = type.GetGenericTypeDefinition();
            
            if (genericType == typeof(ValueTuple<>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,,,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,,,,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,,,,,>))
            {
                return true;
            }

            if (genericType == typeof(ValueTuple<,,,,,,,>))
            {
                return true;
            }
            
            if (genericType == typeof(Tuple<>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,,,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,,,,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,,,,,>))
            {
                return true;
            }

            if (genericType == typeof(Tuple<,,,,,,,>))
            {
                return true;
            }

            return false;
        }
    }
}
