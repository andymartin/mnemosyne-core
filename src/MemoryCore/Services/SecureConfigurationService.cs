using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Models;
using System.Text.Json;
using System.Linq;

namespace Mnemosyne.Core.Services;

public class SecureConfigurationService : ISecureConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecureConfigurationService> _logger;
    private readonly string _localConfigPath;
    private readonly string _secretsPath;

    public SecureConfigurationService(
        IConfiguration configuration,
        ILogger<SecureConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _localConfigPath = Path.Combine(Environment.CurrentDirectory, "appsettings.Local.json");
        _secretsPath = Path.Combine(Environment.CurrentDirectory, "secrets.json");
    }

    public Result<LanguageModelOptions> LoadLanguageModelConfiguration()
    {
        try
        {
            var options = new LanguageModelOptions();
            
            // Load base configuration from appsettings
            var baseConfig = _configuration.GetSection("LanguageModels");
            if (baseConfig.Exists())
            {
                // Dynamically load all configured models
                foreach (var modelSection in baseConfig.GetChildren())
                {
                    var modelName = modelSection.Key;
                    var config = CreateConfigurationFromSection(modelSection, modelName);
                    options.SetConfiguration(modelName, config);
                }
            }
            
            // Override with local configuration if available
            var localConfigResult = LoadLocalConfiguration();
            if (localConfigResult.IsSuccess && localConfigResult.Value != null)
            {
                MergeConfigurations(options, localConfigResult.Value);
            }
            
            // Override API keys from environment variables or secrets file
            OverrideApiKeysFromSecureSources(options);
            
            return Result.Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load language model configuration");
            return Result.Fail<LanguageModelOptions>($"Failed to load configuration: {ex.Message}");
        }
    }

    public Result ValidateConfiguration()
    {
        var configResult = LoadLanguageModelConfiguration();
        if (configResult.IsFailed)
        {
            return Result.Fail("Failed to load configuration for validation");
        }

        var config = configResult.Value;
        var errors = new List<string>();

        // Validate all configured models dynamically
        foreach (var modelConfig in config.GetAllConfigurations())
        {
            if (modelConfig.Enabled)
            {
                var validation = ValidateModelConfiguration(modelConfig, modelConfig.Name);
                if (validation.IsFailed)
                {
                    errors.AddRange(validation.Errors.Select(e => e.Message));
                }
            }
        }

        if (errors.Any())
        {
            return Result.Fail(string.Join("; ", errors));
        }

        return Result.Ok();
    }

    public Result<string> GetApiKey(string providerName)
    {
        // Check environment variables first
        var envVarName = $"MNEMOSYNE_LLM_{providerName.ToUpper()}_API_KEY";
        var apiKey = Environment.GetEnvironmentVariable(envVarName);
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            return Result.Ok(apiKey);
        }

        // Check secrets file
        var secretsResult = LoadSecretsFile();
        if (secretsResult.IsSuccess && secretsResult.Value != null)
        {
            // Try exact match first
            if (secretsResult.Value.TryGetValue($"{providerName}ApiKey", out var secretApiKey))
            {
                return Result.Ok(secretApiKey.ToString() ?? string.Empty);
            }
            
            // Try case-insensitive match
            var keyToFind = $"{providerName}ApiKey";
            var matchingKey = secretsResult.Value.Keys.FirstOrDefault(k =>
                string.Equals(k, keyToFind, StringComparison.OrdinalIgnoreCase));
            
            if (matchingKey != null && secretsResult.Value.TryGetValue(matchingKey, out var caseInsensitiveApiKey))
            {
                return Result.Ok(caseInsensitiveApiKey.ToString() ?? string.Empty);
            }
        }

        return Result.Fail($"API key not found for provider: {providerName}");
    }

    private LanguageModelConfiguration CreateConfigurationFromSection(IConfigurationSection section, string name)
    {
        var config = new LanguageModelConfiguration
        {
            Name = section.GetValue<string>("Name") ?? name,
            Url = section.GetValue<string>("Url") ?? string.Empty,
            MaxTokens = section.GetValue<int>("MaxTokens", 4096),
            ModelName = section.GetValue<string>("ModelName") ?? string.Empty,
            Enabled = section.GetValue<bool>("Enabled", true)
        };

        // Parse provider enum
        if (Enum.TryParse<LlmProvider>(section.GetValue<string>("Provider"), true, out var provider))
        {
            config.Provider = provider;
        }

        // Load additional headers
        var headersSection = section.GetSection("AdditionalHeaders");
        if (headersSection.Exists())
        {
            foreach (var header in headersSection.GetChildren())
            {
                config.AdditionalHeaders[header.Key] = header.Value ?? string.Empty;
            }
        }

        // Load provider-specific settings
        var settingsSection = section.GetSection("ProviderSpecificSettings");
        if (settingsSection.Exists())
        {
            foreach (var setting in settingsSection.GetChildren())
            {
                config.ProviderSpecificSettings[setting.Key] = setting.Value ?? string.Empty;
            }
        }

        return config;
    }

    private Result<Dictionary<string, object>?> LoadLocalConfiguration()
    {
        try
        {
            if (!File.Exists(_localConfigPath))
            {
                return Result.Ok<Dictionary<string, object>?>(null);
            }

            var json = File.ReadAllText(_localConfigPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return Result.Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load local configuration from {Path}", _localConfigPath);
            return Result.Ok<Dictionary<string, object>?>(null);
        }
    }

    private Result<Dictionary<string, object>?> LoadSecretsFile()
    {
        try
        {
            if (!File.Exists(_secretsPath))
            {
                return Result.Ok<Dictionary<string, object>?>(null);
            }

            var json = File.ReadAllText(_secretsPath);
            var secrets = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return Result.Ok(secrets);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load secrets from {Path}", _secretsPath);
            return Result.Ok<Dictionary<string, object>?>(null);
        }
    }

    private void MergeConfigurations(LanguageModelOptions target, Dictionary<string, object> source)
    {
        if (source.TryGetValue("LanguageModels", out var languageModelsObj) &&
            languageModelsObj is JsonElement languageModels)
        {
            // Dynamically merge all model configurations
            foreach (var property in languageModels.EnumerateObject())
            {
                var modelName = property.Name;
                var modelElement = property.Value;
                
                // Create new configuration if it doesn't exist
                if (!target.HasConfiguration(modelName))
                {
                    target.SetConfiguration(modelName, new LanguageModelConfiguration { Name = modelName });
                }
                
                UpdateConfigurationFromJson(target.GetConfiguration(modelName), modelElement);
            }
        }
    }

    private void UpdateConfigurationFromJson(LanguageModelConfiguration config, JsonElement element)
    {
        if (element.TryGetProperty("Name", out var name))
            config.Name = name.GetString() ?? config.Name;
        
        if (element.TryGetProperty("Provider", out var provider) && 
            Enum.TryParse<LlmProvider>(provider.GetString(), true, out var providerEnum))
            config.Provider = providerEnum;
        
        if (element.TryGetProperty("Url", out var url))
            config.Url = url.GetString() ?? config.Url;
        
        
        if (element.TryGetProperty("MaxTokens", out var maxTokens))
            config.MaxTokens = maxTokens.GetInt32();
        
        if (element.TryGetProperty("ModelName", out var modelName))
            config.ModelName = modelName.GetString() ?? config.ModelName;
        
        if (element.TryGetProperty("Enabled", out var enabled))
            config.Enabled = enabled.GetBoolean();
    }

    private void OverrideApiKeysFromSecureSources(LanguageModelOptions options)
    {
        // Override API keys for all configured models dynamically
        foreach (var modelName in options.GetConfiguredModelNames())
        {
            var config = options.GetConfiguration(modelName);
            
            // Try model-specific API key first
            
            // Also check provider-specific environment variables
            OverrideProviderApiKey(config);
        }
    }

    private void OverrideProviderApiKey(LanguageModelConfiguration config)
    {
        var providerName = config.Provider.ToString().ToUpper();
        var envVarName = $"MNEMOSYNE_{providerName}_API_KEY";
        var apiKey = Environment.GetEnvironmentVariable(envVarName);
        
    }

    private Result ValidateModelConfiguration(LanguageModelConfiguration config, string modelType)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(config.Name))
        {
            errors.Add($"{modelType}: Name is required");
        }

        if (string.IsNullOrEmpty(config.Url))
        {
            errors.Add($"{modelType}: URL is required");
        }
        else if (!Uri.TryCreate(config.Url, UriKind.Absolute, out _))
        {
            errors.Add($"{modelType}: URL is not a valid URI");
        }


        if (config.MaxTokens <= 0)
        {
            errors.Add($"{modelType}: MaxTokens must be greater than 0");
        }

        if (string.IsNullOrEmpty(config.ModelName))
        {
            errors.Add($"{modelType}: ModelName is required");
        }

        if (errors.Any())
        {
            return Result.Fail(errors);
        }

        return Result.Ok();
    }
}