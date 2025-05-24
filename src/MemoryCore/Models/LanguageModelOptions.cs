using System;
using System.Collections.Generic;

namespace Mnemosyne.Core.Models;

public class LanguageModelOptions
{
    private readonly Dictionary<LanguageModelType, LanguageModelConfiguration> _configurations = new();

    public LanguageModelOptions()
    {
        _configurations[LanguageModelType.Master] = new LanguageModelConfiguration();
        _configurations[LanguageModelType.Auxiliary] = new LanguageModelConfiguration();
    }

    public LanguageModelConfiguration GetConfiguration(LanguageModelType modelType)
    {
        if (_configurations.TryGetValue(modelType, out var config))
        {
            return config;
        }
        
        throw new KeyNotFoundException($"No configuration found for language model type: {modelType}");
    }

    public void SetConfiguration(LanguageModelType modelType, LanguageModelConfiguration configuration)
    {
        _configurations[modelType] = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    
    public LanguageModelConfiguration Master
    {
        get => GetConfiguration(LanguageModelType.Master);
        set => SetConfiguration(LanguageModelType.Master, value);
    }
    
    public LanguageModelConfiguration Auxiliary
    {
        get => GetConfiguration(LanguageModelType.Auxiliary);
        set => SetConfiguration(LanguageModelType.Auxiliary, value);
    }
}