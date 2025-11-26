// Archivo: Metadata/Conventions/FirestoreNavigationExtensions.cs
using Microsoft.EntityFrameworkCore.Metadata;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public static class FirestoreNavigationExtensions
{
    private const string SubCollectionAnnotation = "Firestore:SubCollection";
    
    /// <summary>
    /// Determina si una navigation es una subcollection de Firestore
    /// </summary>
    public static bool IsSubCollection(this IReadOnlyNavigation navigation)
    {
        return navigation.FindAnnotation(SubCollectionAnnotation)?.Value as bool? == true;
    }
    
    /// <summary>
    /// Marca una navigation como subcollection
    /// </summary>
    public static void SetIsSubCollection(this IMutableNavigation navigation, bool isSubCollection)
    {
        navigation.SetAnnotation(SubCollectionAnnotation, isSubCollection);
    }
}