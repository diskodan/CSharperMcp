namespace CSharperMcp.Server.Extensions;

/// <summary>
/// Extension methods for collection operations.
/// </summary>
internal static class CollectionExtensions
{
    /// <summary>
    /// Returns the enumerable or an empty array if the enumerable is null.
    /// Enables null-safe chaining without the ?. operator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <returns>The source enumerable or an empty array.</returns>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? source) => source ?? [];

    /// <summary>
    /// Returns the list or an empty list if the list is null.
    /// Enables null-safe chaining without the ?. operator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source list.</param>
    /// <returns>The source list or an empty list.</returns>
    public static List<T> OrEmpty<T>(this List<T>? source) => source ?? [];

    /// <summary>
    /// Returns the array or an empty array if the array is null.
    /// Enables null-safe chaining without the ?. operator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source array.</param>
    /// <returns>The source array or an empty array.</returns>
    public static T[] OrEmpty<T>(this T[]? source) => source ?? [];

    /// <summary>
    /// Returns the readonly list or an empty array if the readonly list is null.
    /// Enables null-safe chaining without the ?. operator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source readonly list.</param>
    /// <returns>The source readonly list or an empty array.</returns>
    public static IReadOnlyList<T> OrEmpty<T>(this IReadOnlyList<T>? source) => source ?? [];
}
