using System;
using System.Collections.Generic;

namespace Mnemosyne.Core.Models;

public class LanguageModelOptions
{
    public Dictionary<string, LanguageModelConfiguration> Configurations { get; set; } = new();
    public Dictionary<LanguageModelType, string> DefaultAssignments { get; set; } = new();

    public LanguageModelOptions()
    {
        // Initialize default configurations for backward compatibility
        Configurations["Master"] = new LanguageModelConfiguration { Name = "Master" };
        Configurations["Auxiliary"] = new LanguageModelConfiguration { Name = "Auxiliary" };
        
        // Set default assignments
        DefaultAssignments[LanguageModelType.Master] = "Master";
        DefaultAssignments[LanguageModelType.Auxiliary] = "Auxiliary";
    }

    // Backward compatibility methods
    public LanguageModelConfiguration GetConfiguration(string modelName)
    {
        if (Configurations.TryGetValue(modelName, out var config))
        {
            return config;
        }
        
        throw new KeyNotFoundException($"No configuration found for language model: {modelName}");
    }

    public void SetConfiguration(string modelName, LanguageModelConfiguration configuration)
    {
        Configurations[modelName] = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool HasConfiguration(string modelName)
    {
        return Configurations.ContainsKey(modelName);
    }

    public IEnumerable<string> GetConfiguredModelNames()
    {
        return Configurations.Keys;
    }

    public IEnumerable<LanguageModelConfiguration> GetAllConfigurations()
    {
        return Configurations.Values;
    }

    public void RemoveConfiguration(string modelName)
    {
        Configurations.Remove(modelName);
    }

    // Backward compatibility properties
    public LanguageModelConfiguration Master
    {
        get => GetConfiguration("Master");
        set => SetConfiguration("Master", value);
    }
    
    public LanguageModelConfiguration Auxiliary
    {
        get => GetConfiguration("Auxiliary");
        set => SetConfiguration("Auxiliary", value);
    }

    // Indexer for dynamic access
    public LanguageModelConfiguration this[string modelName]
    {
        get => GetConfiguration(modelName);
        set => SetConfiguration(modelName, value);
    }
}