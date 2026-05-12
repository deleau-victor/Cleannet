using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Infrastructure.AspNetCore.ModelBinding;

/// <summary>
/// Model binder ASP.NET MVC pour les identifiants <see cref="Id"/> et <see cref="Id{TEntity}"/>.
/// Utilise <see cref="ISpanParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
/// via dispatch générique statique (C# 11+).
/// </summary>
internal sealed class IdModelBinder<TId> : IModelBinder
    where TId : struct, ISpanParsable<TId>
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        string? value = valueProviderResult.FirstValue;
        if (string.IsNullOrEmpty(value))
            return Task.CompletedTask;

        if (TId.TryParse(value, CultureInfo.InvariantCulture, out TId id))
        {
            bindingContext.Result = ModelBindingResult.Success(id);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"'{value}' n'est pas un identifiant valide.");
        }

        return Task.CompletedTask;
    }
}
