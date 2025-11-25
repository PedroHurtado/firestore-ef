using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Collections.Concurrent;

namespace Firestore.EntityFrameworkCore.Infrastructure.Internal
{
    public class FirestoreCollectionManager(ILogger<FirestoreCollectionManager> logger) : IFirestoreCollectionManager
    {
        private readonly ILogger<FirestoreCollectionManager> _logger = logger;
        private readonly ConcurrentDictionary<Type, string> _collectionNameCache = new ConcurrentDictionary<Type, string>();

        public string GetCollectionName(Type entityType)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (_collectionNameCache.TryGetValue(entityType, out var cachedName))
                return cachedName;

            var collectionName = DetermineCollectionName(entityType);
            _collectionNameCache[entityType] = collectionName;

            _logger.LogDebug("Colección determinada: {EntityType} -> {CollectionName}",
                entityType.Name, collectionName);

            return collectionName;
        }

        private string DetermineCollectionName(Type entityType)
        {
            // Buscar atributo [Table] de System.ComponentModel.DataAnnotations.Schema
            var tableAttr = entityType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
            if (tableAttr != null && !string.IsNullOrEmpty(tableAttr.Name))
                return tableAttr.Name;

            // Por defecto, pluralizar el nombre del tipo
            var baseName = entityType.Name;
            return Pluralize(baseName);
        }

        private static string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) && 
                name.Length > 1 && 
                !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }

            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char c)
        {
            c = char.ToLowerInvariant(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }
    }
}
