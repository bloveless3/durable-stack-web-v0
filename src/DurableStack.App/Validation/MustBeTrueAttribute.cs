using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DurableStack.App.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public override bool IsValid(object? value)
    {
        return value is bool boolValue && boolValue;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-required"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
    }
}
