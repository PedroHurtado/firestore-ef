// Archivo: Metadata/Conventions/FirestoreNavigationExtensions.cs
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public static class FirestoreNavigationExtensions
{
    private const string SubCollectionAnnotation = "Firestore:SubCollection";
    private const string DocumentReferenceAnnotation = "Firestore:DocumentReference";

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

    /// <summary>
    /// Determina si una navigation es un DocumentReference de Firestore
    /// </summary>
    public static bool IsDocumentReference(this IReadOnlyNavigation navigation)
    {
        return navigation.FindAnnotation(DocumentReferenceAnnotation)?.Value as bool? == true;
    }

    /// <summary>
    /// Marca una navigation como DocumentReference
    /// </summary>
    public static void SetIsDocumentReference(this IMutableNavigation navigation, bool isDocumentReference)
    {
        navigation.SetAnnotation(DocumentReferenceAnnotation, isDocumentReference);
    }

    /// <summary>
    /// Determina si una navigation está configurada para Firestore (SubCollection o DocumentReference)
    /// </summary>
    public static bool IsFirestoreConfigured(this IReadOnlyNavigation navigation)
    {
        return navigation.IsSubCollection() || navigation.IsDocumentReference();
    }
}