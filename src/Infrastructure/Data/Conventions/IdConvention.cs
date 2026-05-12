using Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data.Conventions;

/// <summary>
/// Convention EF Core appliquant le mapping ULID-as-char(26) à toute propriété
/// dont le type CLR est <see cref="Id"/>, <see cref="Id"/>?, ou <see cref="Id{TEntity}"/>.
/// </summary>
internal sealed class IdConvention : IModelFinalizingConvention
{
    private const string ColumnType = "char(26)";
    private const int Length = 26;
    private const string BinaryCollation = "C";

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (IConventionProperty property in entityType.GetDeclaredProperties())
            {
                ValueConverter? converter = ResolveConverter(property.ClrType);
                if (converter is null)
                    continue;

                property.SetValueConverter(converter);
                property.SetColumnType(ColumnType);
                property.SetIsUnicode(false);
                property.SetMaxLength(Length);
                property.SetCollation(BinaryCollation);
            }
        }
    }

    private static ValueConverter? ResolveConverter(Type clrType)
    {
        Type underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlying == typeof(Id))
            return new IdConverter();

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(Id<>))
        {
            Type converterType = typeof(IdConverter<>).MakeGenericType(underlying.GetGenericArguments()[0]);
            return (ValueConverter)Activator.CreateInstance(converterType)!;
        }

        return null;
    }
}
