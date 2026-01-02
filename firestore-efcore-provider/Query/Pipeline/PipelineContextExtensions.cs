namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Extension methods for working with pipeline context metadata.
/// </summary>
public static class PipelineContextExtensions
{
    /// <summary>
    /// Adds or updates a metadata value in the context.
    /// Returns a new context with the updated metadata.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="context">The pipeline context.</param>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A new context with the updated metadata.</returns>
    public static PipelineContext WithMetadata<T>(
        this PipelineContext context,
        MetadataKey<T> key,
        T value)
    {
        return context with
        {
            Metadata = context.Metadata.SetItem(key.Name, value!)
        };
    }

    /// <summary>
    /// Gets a metadata value from the context.
    /// Returns default if the key is not found.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="context">The pipeline context.</param>
    /// <param name="key">The metadata key.</param>
    /// <returns>The value if found, otherwise default.</returns>
    public static T? GetMetadata<T>(this PipelineContext context, MetadataKey<T> key)
    {
        return context.Metadata.TryGetValue(key.Name, out var value)
            ? (T)value
            : default;
    }
}
