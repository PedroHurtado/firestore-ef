using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Converters;

/// <summary>
/// Converter para List&lt;TEnum&gt; → List&lt;string&gt;
/// </summary>
public class ListEnumToStringConverter<TEnum> : ValueConverter<List<TEnum>, List<string>>
    where TEnum : struct, Enum
{
    public ListEnumToStringConverter()
        : base(
            v => v.Select(e => e.ToString()).ToList(),
            v => v.Select(s => Enum.Parse<TEnum>(s)).ToList()
        )
    {
    }
}
