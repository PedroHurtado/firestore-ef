using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Firestore.EntityFrameworkCore.Metadata.Converters;

/// <summary>
/// Converter para List&lt;TEnum?&gt; → List&lt;string?&gt;
/// </summary>
public class ListNullableEnumToStringConverter<TEnum> : ValueConverter<List<TEnum?>, List<string?>>
    where TEnum : struct, Enum
{
    public ListNullableEnumToStringConverter()
        : base(
            v => v.Select(e => e.HasValue ? e.Value.ToString() : null).ToList(),
            v => v.Select(s => string.IsNullOrEmpty(s) ? (TEnum?)null : Enum.Parse<TEnum>(s)).ToList()
        )
    {
    }
}
