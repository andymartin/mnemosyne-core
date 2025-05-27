using System;
using System.Collections.Generic;

namespace Mnemosyne.Core.Models;

public class LanguageModelOptions
{
    private readonly Dictionary<string, LanguageModelConfiguration> _configurations = new();

    public LanguageModelOptions()
    {
        // Initialize with default configurations for backward compatibility
        _configurations["Master"] = new LanguageModelConfiguration { Name = "Master" };
        _configurations["Auxiliary"] = new LanguageModelConfiguration { Name = "Auxiliary" };
    }

    public LanguageModelConfiguration GetConfiguration(string modelName)
    {
        if (_configurations.TryGetValue(modelName, out var config))
        {
            return config;
        }
        
        throw new KeyNotFoundException($"No configuration found for language model: {modelName}");
    }

    public void SetConfiguration(string modelName, LanguageModelConfiguration configuration)
    {
        _configurations[modelName] = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public bool HasConfiguration(string modelName)
    {
        return _configurations.ContainsKey(modelName);
    }

    public IEnumerable<string> GetConfiguredModelNames()
    {
        return _configurations.Keys;
    }

    public IEnumerable<LanguageModelConfiguration> GetAllConfigurations()
    {
        return _configurations.Values;
    }

    public void RemoveConfiguration(string modelName)
    {
        _configurations.Remove(modelName);
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