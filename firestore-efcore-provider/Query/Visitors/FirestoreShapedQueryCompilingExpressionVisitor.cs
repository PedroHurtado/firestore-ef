using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
    {
        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Los includes ya fueron capturados en TranslateSelect
            PrintIncludesSummary(firestoreQueryExpression);

            var entityType = firestoreQueryExpression.EntityType.ClrType;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");

            var shaperExpression = CreateShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                firestoreQueryExpression);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter);

            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(entityType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Func<,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), entityType),
                typeof(Type)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType));

            return newExpression;
        }

        #region Shaper Creation

        private Expression CreateShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            FirestoreQueryExpression queryExpression)
        {
            var entityType = queryExpression.EntityType.ClrType;
            var deserializeMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeEntity), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType);

            return Expression.Call(
                deserializeMethod,
                queryContextParameter,
                documentSnapshotParameter,
                Expression.Constant(queryExpression));
        }

        private static T DeserializeEntity<T>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            FirestoreQueryExpression queryExpression) where T : class, new()
        {
            var dbContext = queryContext.Context;
            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;

            var model = dbContext.Model;
            var typeMappingSource = (ITypeMappingSource)serviceProvider.GetService(typeof(ITypeMappingSource))!;
            var collectionManager = (IFirestoreCollectionManager)serviceProvider.GetService(typeof(IFirestoreCollectionManager))!;
            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory))!;
            var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;

            var deserializerLogger = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<Storage.FirestoreDocumentDeserializer>(loggerFactory);
            var deserializer = new Storage.FirestoreDocumentDeserializer(
                model,
                typeMappingSource,
                collectionManager,
                deserializerLogger);

            var entity = deserializer.DeserializeEntity<T>(documentSnapshot);

            // Cargar includes
            if (queryExpression.PendingIncludes.Count > 0)
            {
                LoadIncludes(entity, documentSnapshot, queryExpression.PendingIncludes, clientWrapper, deserializer, model)
                    .GetAwaiter().GetResult();
            }

            return entity;
        }

        #endregion

        #region Include Loading

        private static async Task LoadIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model) where T : class
        {
            var rootNavigations = allIncludes
                .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
                .ToList();

            var tasks = rootNavigations.Select(navigation =>
                LoadNavigationAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model));

            await Task.WhenAll(tasks);
        }

        private static async Task LoadNavigationAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            if (navigation.IsCollection)
            {
                await LoadSubCollectionAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model);
            }
            else
            {
                await LoadReferenceAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model);
            }
        }

        private static async Task LoadSubCollectionAsync(
            object parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            if (!navigation.IsSubCollection())
            {
                Console.WriteLine($"⚠ Navigation '{navigation.Name}' is not a subcollection, skipping");
                return;
            }

            var subCollectionName = GetSubCollectionName(navigation);
            var subCollectionRef = parentDoc.Reference.Collection(subCollectionName);

            Console.WriteLine($"📂 Loading subcollection: {parentDoc.Reference.Path}/{subCollectionName}");

            var snapshot = await subCollectionRef.GetSnapshotAsync();

            var listType = typeof(List<>).MakeGenericType(navigation.TargetEntityType.ClrType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethod(nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity))!
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists)
                    continue;

                var childEntity = deserializeMethod.Invoke(deserializer, new object[] { doc });
                if (childEntity == null)
                    continue;

                var childIncludes = allIncludes
                    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    Console.WriteLine($"  🔁 Loading {childIncludes.Count} nested include(s) for {navigation.TargetEntityType.ClrType.Name}");

                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        childEntity, doc, allIncludes, clientWrapper, deserializer, model
                    })!;
                }

                ApplyFixup(parentEntity, childEntity, navigation);

                list.Add(childEntity);
            }

            navigation.PropertyInfo?.SetValue(parentEntity, list);
            Console.WriteLine($"✅ Loaded {list.Count} item(s) for {navigation.Name}");
        }

        private static async Task LoadReferenceAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            Console.WriteLine($"🔗 Loading reference: {navigation.Name}");

            var data = documentSnapshot.ToDictionary();

            object? referenceValue = null;

            if (data.TryGetValue(navigation.Name, out var directValue))
            {
                referenceValue = directValue;
            }
            else if (data.TryGetValue($"{navigation.Name}Id", out var idValue))
            {
                referenceValue = idValue;
            }

            if (referenceValue == null)
            {
                Console.WriteLine($"⚠ Reference field not found for {navigation.Name}");
                return;
            }

            DocumentSnapshot? referencedDoc = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                Console.WriteLine($"  → Found DocumentReference: {docRef.Path}");
                referencedDoc = await docRef.GetSnapshotAsync();
            }
            else if (referenceValue is string id)
            {
                var targetEntityType = model.FindEntityType(navigation.TargetEntityType.ClrType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    Console.WriteLine($"  → Constructed reference from ID: {docRefFromId.Path}");
                    referencedDoc = await docRefFromId.GetSnapshotAsync();
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
            {
                Console.WriteLine($"⚠ Referenced document not found");
                return;
            }

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethod(nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity))!
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            var referencedEntity = deserializeMethod.Invoke(deserializer, new object[] { referencedDoc });

            if (referencedEntity != null)
            {
                var childIncludes = allIncludes
                    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    Console.WriteLine($"  🔁 Loading {childIncludes.Count} nested include(s) for reference {navigation.Name}");

                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        referencedEntity, referencedDoc, allIncludes, clientWrapper, deserializer, model
                    })!;
                }

                ApplyFixup(entity, referencedEntity, navigation);

                navigation.PropertyInfo?.SetValue(entity, referencedEntity);
                Console.WriteLine($"✅ Loaded reference {navigation.Name}");
            }
        }

        private static void ApplyFixup(
            object parent,
            object child,
            IReadOnlyNavigation navigation)
        {
            if (navigation.Inverse != null)
            {
                var inverseProperty = navigation.Inverse.PropertyInfo;
                if (inverseProperty != null)
                {
                    if (navigation.IsCollection)
                    {
                        inverseProperty.SetValue(child, parent);
                        Console.WriteLine($"  🔗 Fixup: {child.GetType().Name}.{inverseProperty.Name} → {parent.GetType().Name}");
                    }
                    else
                    {
                        if (navigation.Inverse.IsCollection)
                        {
                            var collection = inverseProperty.GetValue(parent) as System.Collections.IList;
                            if (collection != null && !collection.Contains(child))
                            {
                                collection.Add(child);
                                Console.WriteLine($"  🔗 Fixup: Added to {parent.GetType().Name}.{inverseProperty.Name}");
                            }
                        }
                        else
                        {
                            inverseProperty.SetValue(parent, child);
                            Console.WriteLine($"  🔗 Fixup: {parent.GetType().Name}.{inverseProperty.Name} → {child.GetType().Name}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static string GetSubCollectionName(IReadOnlyNavigation navigation)
        {
            var childEntityType = navigation.ForeignKey.DeclaringEntityType;

            // Pluralizar el nombre del tipo de entidad
            return Pluralize(childEntityType.ClrType.Name);
        }

        private static string GetCollectionNameForEntityType(IEntityType entityType)
        {
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            return Pluralize(entityType.ClrType.Name);
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

        private void PrintIncludesSummary(FirestoreQueryExpression queryExpression)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
            Console.WriteLine("║         RESUMEN DE INCLUDES DETECTADOS                ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
            Console.WriteLine($"Total PendingIncludes: {queryExpression.PendingIncludes.Count}\n");

            if (queryExpression.PendingIncludes.Count == 0)
            {
                Console.WriteLine("⚠ ⚠ ⚠  NO SE DETECTÓ NINGÚN INCLUDE  ⚠ ⚠ ⚠\n");
            }
            else
            {
                var grouped = queryExpression.PendingIncludes
                    .GroupBy(n => n.DeclaringEntityType.ClrType.Name)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    Console.WriteLine($"  📁 {group.Key}:");
                    foreach (var nav in group)
                    {
                        var typeIndicator = nav.IsCollection ? "[Collection]" : "[Reference]";
                        var isSubColl = nav.IsSubCollection() ? "✓ SubCollection" : "⚠ NOT SubCollection";
                        Console.WriteLine($"    └─{typeIndicator} {nav.Name} → {nav.TargetEntityType.ClrType.Name} ({isSubColl})");
                    }
                }

                Console.WriteLine($"\n  📊 Árbol de carga esperado:");
                PrintLoadingTree(queryExpression.PendingIncludes);
            }

            Console.WriteLine($"\n═══════════════════════════════════════════════════════\n");
        }

        private void PrintLoadingTree(List<IReadOnlyNavigation> navigations)
        {
            var allTargetTypes = new HashSet<IReadOnlyEntityType>(
                navigations.Select(n => n.TargetEntityType));

            var rootTypes = navigations
                .Select(n => n.DeclaringEntityType)
                .Distinct()
                .Where(t => !allTargetTypes.Contains(t))
                .ToList();

            foreach (var rootType in rootTypes)
            {
                Console.WriteLine($"  {rootType.ClrType.Name}");
                PrintNavigationChildren(rootType, navigations, indent: "    ");
            }
        }

        private void PrintNavigationChildren(
            IReadOnlyEntityType entityType,
            List<IReadOnlyNavigation> allNavigations,
            string indent)
        {
            var children = allNavigations
                .Where(n => n.DeclaringEntityType == entityType)
                .ToList();

            foreach (var child in children)
            {
                var indicator = child.IsCollection ? "└─[1:N]" : "└─[N:1]";
                Console.WriteLine($"{indent}{indicator} {child.Name} → {child.TargetEntityType.ClrType.Name}");

                PrintNavigationChildren(child.TargetEntityType, allNavigations, indent + "    ");
            }
        }

        #endregion
    }
}
