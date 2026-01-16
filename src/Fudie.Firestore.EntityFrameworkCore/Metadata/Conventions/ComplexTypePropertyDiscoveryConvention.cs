using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convención que descubre propiedades en ComplexTypes (Value Objects) incluso cuando
/// tienen constructores protected sin constructor sin parámetros.
///
/// WHY: EF Core's ConstructorBindingConvention bloquea records con constructores protected
/// parametrizados porque no puede crear un binding. Sin embargo, Firestore tiene su propio
/// deserializador (FirestoreDocumentDeserializer) que SÍ puede instanciar estos tipos
/// usando reflection.
///
/// Al remover ConstructorBindingConvention, EF Core no descubre automáticamente las propiedades
/// de los ComplexTypes, resultando en "Complex type has no properties defined".
///
/// WHAT: Esta convención descubre manualmente las propiedades públicas de los ComplexTypes
/// que tienen getter, permitiendo que records DDD como:
///
///   public record DepositPolicy {
///       public DepositType DepositType { get; }
///       protected DepositPolicy(DepositType depositType) => DepositType = depositType;
///   }
///
/// funcionen correctamente como Value Objects.
///
/// REFERENCE: Similar al comportamiento de PropertyDiscoveryConvention pero específico
/// para ComplexTypes que no pueden ser enlazados por ConstructorBindingConvention.
/// </summary>
public class ComplexTypePropertyDiscoveryConvention : IComplexPropertyAddedConvention
{
    public void ProcessComplexPropertyAdded(
        IConventionComplexPropertyBuilder propertyBuilder,
        IConventionContext<IConventionComplexPropertyBuilder> context)
    {
        var complexType = propertyBuilder.Metadata.ComplexType;
        DiscoverProperties(complexType);
    }

    /// <summary>
    /// Descubre y registra las propiedades públicas del ComplexType.
    /// </summary>
    private static void DiscoverProperties(IConventionComplexType complexType)
    {
        var clrType = complexType.ClrType;
        var builder = complexType.Builder;

        // Obtener todas las propiedades públicas con getter
        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Where(p => !IsIgnoredType(p.PropertyType))
            .Where(p => !IsComplexOrNavigationType(p, complexType));

        foreach (var propertyInfo in properties)
        {
            // Verificar si la propiedad ya está configurada
            if (complexType.FindProperty(propertyInfo.Name) != null)
            {
                continue;
            }

            // Verificar si la propiedad está explícitamente ignorada
            if (complexType.IsIgnored(propertyInfo.Name))
            {
                continue;
            }

            // Intentar agregar la propiedad al modelo
            try
            {
                builder.Property(propertyInfo);
            }
            catch
            {
                // Si falla (por ejemplo, tipo no soportado), ignorar silenciosamente
                // EF Core validará esto más tarde si es necesario
            }
        }
    }

    /// <summary>
    /// Determina si un tipo debe ser ignorado (colecciones, diccionarios, etc.).
    /// </summary>
    private static bool IsIgnoredType(Type type)
    {
        // Ignorar tipos que no deben ser propiedades escalares
        if (type.IsArray && type != typeof(byte[]))
            return true;

        // Ignorar colecciones genéricas (excepto las que tienen tratamiento especial)
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();

            // Ignorar colecciones
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(HashSet<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(IReadOnlyCollection<>) ||
                genericDef == typeof(IReadOnlyList<>) ||
                genericDef == typeof(Dictionary<,>) ||
                genericDef == typeof(IDictionary<,>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determina si una propiedad es una navegación o ComplexType anidado.
    /// </summary>
    private static bool IsComplexOrNavigationType(PropertyInfo propertyInfo, IConventionComplexType complexType)
    {
        var propertyType = propertyInfo.PropertyType;
        var model = complexType.Model;

        // Si es una entidad en el modelo, es una navegación
        if (model.FindEntityType(propertyType) != null)
        {
            return true;
        }

        // Si es un ComplexType configurado, dejarlo para otra convención
        if (complexType.FindComplexProperty(propertyInfo.Name) != null)
        {
            return true;
        }

        return false;
    }
}
