using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data.Converters;

internal sealed class IdConverter : ValueConverter<Id, string>
{
    public IdConverter() : base(
       id => id.ToString(),
       value => Id.Parse(value, null)
    )
    { }
}

internal sealed class IdConverter<TEntity> : ValueConverter<Id<TEntity>, string>
{
    public IdConverter() : base(
        id => id.ToString(),
        value => Id<TEntity>.Parse(value, null))
    { }
}
