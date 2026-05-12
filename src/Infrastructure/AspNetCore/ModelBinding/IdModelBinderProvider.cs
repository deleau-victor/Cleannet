using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Infrastructure.AspNetCore.ModelBinding;

/// <summary>
/// Provider qui fournit un <see cref="IdModelBinder{TId}"/> pour les types
/// <see cref="Id"/> et <see cref="Id{TEntity}"/> (fermés).
/// </summary>
/// <remarks>
/// À insérer en tête de <c>MvcOptions.ModelBinderProviders</c> afin d'avoir priorité
/// sur le <c>SimpleTypeModelBinderProvider</c> qui passerait sinon par le <c>TypeConverter</c>.
/// </remarks>
internal sealed class IdModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Type modelType = context.Metadata.ModelType;

        if (modelType == typeof(Id))
            return new IdModelBinder<Id>();

        if (modelType.IsGenericType && modelType.GetGenericTypeDefinition() == typeof(Id<>))
        {
            Type binderType = typeof(IdModelBinder<>).MakeGenericType(modelType);
            return (IModelBinder)Activator.CreateInstance(binderType)!;
        }

        return null;
    }
}
