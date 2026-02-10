using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCorePoc.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Recursively includes all navigation properties for the given entity type,
    /// using EF Core's IModel metadata to discover the entity graph.
    ///
    /// Inverse navigation filtering: skips "back-pointers" (e.g. Book.Author when
    /// coming from Author.Books) because EF Core auto-populates them via fix-up.
    ///
    /// Cycle detection: visited entity types are tracked per branch to prevent
    /// infinite loops (e.g. Book → Tag → Book).
    ///
    /// Depth limit: throws InvalidOperationException if the entity graph
    /// exceeds maxDepth levels. This is a safety mechanism to prevent
    /// accidentally loading the entire database.
    /// </summary>
    /// <param name="query">The IQueryable to add includes to</param>
    /// <param name="db">The DbContext (needed for IModel metadata access)</param>
    /// <param name="maxDepth">Maximum include depth (default: 3). Throws if exceeded.</param>
    /// <returns>IQueryable with all navigation properties included recursively</returns>
    public static IQueryable<T> IncludeAll<T>(this IQueryable<T> query, DbContext db, int maxDepth = 3)
        where T : class
    {
        var entityType = db.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(T).Name}' is not part of the DbContext model.");

        var includePaths = new List<string>();
        var visited = new HashSet<IEntityType>();

        CollectIncludePaths(entityType, "", 0, maxDepth, visited, includePaths, previousNavigation: null);

        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }

        return query;
    }

    /// <summary>
    /// Returns the discovered include paths without applying them.
    /// Useful for debugging/logging which paths IncludeAll would generate.
    /// </summary>
    public static IReadOnlyList<string> GetIncludeAllPaths<T>(DbContext db, int maxDepth = 3)
        where T : class
    {
        var entityType = db.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(T).Name}' is not part of the DbContext model.");

        var includePaths = new List<string>();
        var visited = new HashSet<IEntityType>();

        CollectIncludePaths(entityType, "", 0, maxDepth, visited, includePaths, previousNavigation: null);

        return includePaths.AsReadOnly();
    }

    private static void CollectIncludePaths(
        IEntityType entityType,
        string currentPath,
        int currentDepth,
        int maxDepth,
        HashSet<IEntityType> visited,
        List<string> includePaths,
        INavigationBase? previousNavigation)
    {
        // Cycle detection: if we've already visited this entity type in this branch, stop
        if (!visited.Add(entityType))
            return;

        // Process regular navigations (1:N, N:1)
        foreach (var navigation in entityType.GetNavigations())
        {
            // Skip inverse navigations (back-pointers like Book.Author when coming from Author.Books)
            // EF Core auto-populates these via fix-up, and including them causes warnings/errors
            if (previousNavigation is INavigation prevNav && navigation.Inverse == prevNav)
                continue;

            ProcessNavigation(navigation, currentPath, currentDepth, maxDepth, visited, includePaths);
        }

        // Process skip navigations (M:N, e.g. Book.Tags, Tag.Books)
        foreach (var skipNav in entityType.GetSkipNavigations())
        {
            // Skip inverse skip navigations (e.g. Tag.Books when coming from Book.Tags)
            if (previousNavigation is ISkipNavigation prevSkip && skipNav.Inverse == prevSkip)
                continue;

            ProcessNavigation(skipNav, currentPath, currentDepth, maxDepth, visited, includePaths);
        }

        // Remove from visited when backtracking (allows the same type in different branches)
        visited.Remove(entityType);
    }

    private static void ProcessNavigation(
        INavigationBase navigation,
        string currentPath,
        int currentDepth,
        int maxDepth,
        HashSet<IEntityType> visited,
        List<string> includePaths)
    {
        var targetType = navigation.TargetEntityType;
        var newPath = string.IsNullOrEmpty(currentPath)
            ? navigation.Name
            : $"{currentPath}.{navigation.Name}";

        var nextDepth = currentDepth + 1;

        // Depth limit check: throw if we'd exceed maxDepth
        if (nextDepth > maxDepth)
        {
            throw new InvalidOperationException(
                $"IncludeAll depth limit exceeded! " +
                $"Navigation path '{newPath}' would reach depth {nextDepth}, " +
                $"but maxDepth is {maxDepth}. " +
                $"Increase maxDepth or restructure your query.");
        }

        includePaths.Add(newPath);

        // Recurse into the target entity's navigations
        CollectIncludePaths(targetType, newPath, nextDepth, maxDepth, visited, includePaths, navigation);
    }
}
