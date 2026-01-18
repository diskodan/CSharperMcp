using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.Services;

/// <summary>
/// Service for discovering CodeFixProviders and CodeRefactoringProviders using MEF-based discovery.
/// Uses the OmniSharp pattern of discovering providers from analyzer assemblies via reflection.
/// </summary>
internal class CodeActionProviderService(ILogger<CodeActionProviderService> logger)
{
    // Cache providers per project for performance
    private readonly ConcurrentDictionary<ProjectId, List<CodeFixProvider>> _codeFixProvidersCache = new();
    private readonly ConcurrentDictionary<ProjectId, List<CodeRefactoringProvider>> _refactoringProvidersCache = new();

    /// <summary>
    /// Gets all CodeFixProviders available for the given project.
    /// Providers are discovered from analyzer references and cached per project.
    /// </summary>
    public List<CodeFixProvider> GetCodeFixProviders(Project project)
    {
        return _codeFixProvidersCache.GetOrAdd(project.Id, _ => DiscoverCodeFixProviders(project));
    }

    /// <summary>
    /// Gets all CodeRefactoringProviders available for the given project.
    /// Providers are discovered from analyzer references and cached per project.
    /// </summary>
    public List<CodeRefactoringProvider> GetCodeRefactoringProviders(Project project)
    {
        return _refactoringProvidersCache.GetOrAdd(project.Id, _ => DiscoverCodeRefactoringProviders(project));
    }

    /// <summary>
    /// Clears the provider cache for a specific project.
    /// </summary>
    public void ClearCache(ProjectId projectId)
    {
        _codeFixProvidersCache.TryRemove(projectId, out _);
        _refactoringProvidersCache.TryRemove(projectId, out _);
    }

    /// <summary>
    /// Clears all provider caches.
    /// </summary>
    public void ClearAllCaches()
    {
        _codeFixProvidersCache.Clear();
        _refactoringProvidersCache.Clear();
    }

    private List<CodeFixProvider> DiscoverCodeFixProviders(Project project)
    {
        logger.LogInformation("Discovering CodeFixProviders for project {ProjectName}", project.Name);

        var providers = new List<CodeFixProvider>();

        // Discover from analyzer references
        foreach (var analyzerReference in project.AnalyzerReferences.OfType<AnalyzerFileReference>())
        {
            try
            {
                var assembly = analyzerReference.GetAssembly();
                if (assembly == null)
                {
                    logger.LogDebug("Skipping analyzer reference {Path} - assembly not loaded", analyzerReference.FullPath);
                    continue;
                }

                var providerTypes = assembly.GetTypes()
                    .Where(type => !type.IsAbstract &&
                                   typeof(CodeFixProvider).IsAssignableFrom(type));

                foreach (var providerType in providerTypes)
                {
                    try
                    {
                        // Check for ExportCodeFixProviderAttribute
                        var attribute = providerType.GetCustomAttribute<ExportCodeFixProviderAttribute>(inherit: false);
                        if (attribute == null)
                        {
                            logger.LogDebug("Skipping {TypeName} - no ExportCodeFixProviderAttribute", providerType.Name);
                            continue;
                        }

                        // Check language compatibility
                        if (attribute.Languages != null &&
                            !attribute.Languages.Contains(project.Language))
                        {
                            logger.LogDebug("Skipping {TypeName} - incompatible language", providerType.Name);
                            continue;
                        }

                        // Instantiate provider
                        var provider = (CodeFixProvider)Activator.CreateInstance(providerType)!;
                        providers.Add(provider);

                        logger.LogDebug("Loaded CodeFixProvider: {TypeName} fixing {DiagnosticIds}",
                            providerType.Name,
                            string.Join(", ", provider.FixableDiagnosticIds));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to instantiate CodeFixProvider {TypeName}", providerType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load assembly for analyzer reference {Path}", analyzerReference.FullPath);
            }
        }

        logger.LogInformation("Discovered {Count} CodeFixProviders for project {ProjectName}",
            providers.Count,
            project.Name);

        return providers;
    }

    private List<CodeRefactoringProvider> DiscoverCodeRefactoringProviders(Project project)
    {
        logger.LogInformation("Discovering CodeRefactoringProviders for project {ProjectName}", project.Name);

        var providers = new List<CodeRefactoringProvider>();

        // Discover from analyzer references
        foreach (var analyzerReference in project.AnalyzerReferences.OfType<AnalyzerFileReference>())
        {
            try
            {
                var assembly = analyzerReference.GetAssembly();
                if (assembly == null)
                {
                    logger.LogDebug("Skipping analyzer reference {Path} - assembly not loaded", analyzerReference.FullPath);
                    continue;
                }

                var providerTypes = assembly.GetTypes()
                    .Where(type => !type.IsAbstract &&
                                   typeof(CodeRefactoringProvider).IsAssignableFrom(type));

                foreach (var providerType in providerTypes)
                {
                    try
                    {
                        // Check for ExportCodeRefactoringProviderAttribute
                        var attribute = providerType.GetCustomAttribute<ExportCodeRefactoringProviderAttribute>(inherit: false);
                        if (attribute == null)
                        {
                            logger.LogDebug("Skipping {TypeName} - no ExportCodeRefactoringProviderAttribute", providerType.Name);
                            continue;
                        }

                        // Check language compatibility
                        if (attribute.Languages != null &&
                            !attribute.Languages.Contains(project.Language))
                        {
                            logger.LogDebug("Skipping {TypeName} - incompatible language", providerType.Name);
                            continue;
                        }

                        // Instantiate provider
                        var provider = (CodeRefactoringProvider)Activator.CreateInstance(providerType)!;
                        providers.Add(provider);

                        logger.LogDebug("Loaded CodeRefactoringProvider: {TypeName}", providerType.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to instantiate CodeRefactoringProvider {TypeName}", providerType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load assembly for analyzer reference {Path}", analyzerReference.FullPath);
            }
        }

        logger.LogInformation("Discovered {Count} CodeRefactoringProviders for project {ProjectName}",
            providers.Count,
            project.Name);

        return providers;
    }
}
