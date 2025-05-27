# Mnemosyne Core - Secure Configuration Guide

This guide explains how to securely configure Language Model (LLM) API keys and settings for Mnemosyne Core without committing sensitive information to git.

## Overview

Mnemosyne Core uses a layered configuration system that separates **roles** from **providers**:

### Roles vs Providers
- **Roles** define the purpose/function (Master, Auxiliary, Evaluator, Creative, etc.)
- **Providers** define the API/service (OpenAI, Anthropic, OpenRouter, Ollama, etc.)
- **Any provider can be used for any role** - complete flexibility

### Features
- Multiple LLM providers (OpenAI, Anthropic, OpenRouter, Ollama, LM Studio, etc.)
- Dynamic role configuration (no hardcoded limitations)
- Local configuration files (git-ignored)
- Environment variables for Docker deployments
- Secure secrets management

## Configuration Hierarchy

Configuration is loaded in the following order (later sources override earlier ones):

1. **Base Configuration** (`appsettings.json`) - Default settings, no API keys
2. **Local Configuration** (`appsettings.Local.json`) - Local overrides, git-ignored
3. **Secrets File** (`secrets.json`) - API keys only, git-ignored
4. **Environment Variables** - For Docker/production deployments

## Quick Setup

### 1. Create Local Configuration

Copy the template and customize:

```bash
# In mnemosyne-core/src/MemoryCore/
cp appsettings.Local.json.template appsettings.Local.json
```

Edit `appsettings.Local.json` with your preferred settings (URLs, model names, etc.).

### 2. Create Secrets File

Copy the template and add your API keys:

```bash
# In mnemosyne-core/src/MemoryCore/
cp secrets.json.template secrets.json
```

Edit `secrets.json` with your actual API keys:

```json
{
  "MasterApiKey": "sk-your-openai-key-here",
  "AuxiliaryApiKey": "sk-ant-your-anthropic-key-here",
  "OpenAIApiKey": "sk-your-openai-key-here",
  "AnthropicApiKey": "sk-ant-your-anthropic-key-here"
}
```

### 3. Alternative: Use Environment Variables

Set environment variables for API keys:

```bash
# Model-specific keys
export MNEMOSYNE_LLM_MASTER_API_KEY="sk-your-openai-key-here"
export MNEMOSYNE_LLM_AUXILIARY_API_KEY="sk-ant-your-anthropic-key-here"

# Provider-specific keys (override model-specific)
export MNEMOSYNE_OPENAI_API_KEY="sk-your-openai-key-here"
export MNEMOSYNE_ANTHROPIC_API_KEY="sk-ant-your-anthropic-key-here"
export MNEMOSYNE_OPENROUTER_API_KEY="sk-or-your-openrouter-key-here"
export MNEMOSYNE_AZURE_OPENAI_API_KEY="your-azure-key-here"
```

## Supported LLM Providers

### OpenAI
```json
{
  "Name": "OpenAI GPT-4",
  "Provider": "OpenAI",
  "Url": "https://api.openai.com",
  "ModelName": "gpt-4",
  "ApiKey": "sk-your-key-here"
}
```

### Anthropic Claude
```json
{
  "Name": "Claude 3 Sonnet",
  "Provider": "Anthropic",
  "Url": "https://api.anthropic.com",
  "ModelName": "claude-3-sonnet-20240229",
  "ApiKey": "sk-ant-your-key-here",
  "AdditionalHeaders": {
    "anthropic-version": "2023-06-01"
  }
}
```

### OpenRouter
OpenRouter provides access to multiple LLM providers through a single API, making it easy to switch between different models.

```json
{
  "Name": "OpenRouter GPT-4",
  "Provider": "OpenRouter",
  "Url": "https://openrouter.ai/api/v1",
  "ModelName": "openai/gpt-4-turbo",
  "ApiKey": "sk-or-your-key-here",
  "AdditionalHeaders": {
    "HTTP-Referer": "https://your-site.com",
    "X-Title": "Mnemosyne Core"
  },
  "ProviderSpecificSettings": {
    "temperature": "0.7"
  }
}
```

Popular OpenRouter model names:
- `openai/gpt-4-turbo`
- `openai/gpt-3.5-turbo`
- `anthropic/claude-3-sonnet`
- `anthropic/claude-3-haiku`
- `meta-llama/llama-2-70b-chat`
- `mistralai/mixtral-8x7b-instruct`
- `google/gemini-pro`

### Ollama (Local)
```json
{
  "Name": "Local Llama",
  "Provider": "Ollama",
  "Url": "http://localhost:11434",
  "ModelName": "llama2",
  "ApiKey": "",
  "ProviderSpecificSettings": {
    "temperature": "0.7",
    "num_predict": "4096"
  }
}
```

### LM Studio (Local)
```json
{
  "Name": "LM Studio Local",
  "Provider": "LmStudio",
  "Url": "http://localhost:1234",
  "ModelName": "local-model",
  "ApiKey": "lm-studio"
}
```

### Azure OpenAI
```json
{
  "Name": "Azure OpenAI",
  "Provider": "AzureOpenAI",
  "Url": "https://your-resource.openai.azure.com",
  "ModelName": "gpt-4",
  "ApiKey": "your-azure-key-here",
  "ProviderSpecificSettings": {
    "api-version": "2023-12-01-preview"
  }
}
```

## Adding New Roles

You can add any number of roles dynamically. The role name becomes the identifier you use in code:

```json
{
  "LanguageModels": {
    "Summarizer": {
      "Name": "Summarizer Role - Document Processing",
      "Provider": "OpenRouter",
      "Url": "https://openrouter.ai/api/v1",
      "ModelName": "openai/gpt-3.5-turbo",
      "ApiKey": "your-openrouter-key",
      "MaxTokens": 4096,
      "Enabled": true,
      "ProviderSpecificSettings": {
        "temperature": "0.3"
      }
    },
    "Translator": {
      "Name": "Translator Role - Language Processing",
      "Provider": "Anthropic",
      "Url": "https://api.anthropic.com",
      "ModelName": "claude-3-haiku-20240307",
      "ApiKey": "your-anthropic-key",
      "MaxTokens": 4096,
      "Enabled": true
    }
  }
}
```

Then use them in code:
```csharp
// Use any role with any provider
await languageModelService.GenerateCompletionAsync(request, "Summarizer");
await languageModelService.GenerateCompletionAsync(request, "Translator");
```

## Switching Providers for Existing Roles

Want to use OpenRouter for your Master role instead of OpenAI? Just change the provider:

```json
{
  "LanguageModels": {
    "Master": {
      "Name": "Master Role - Now using OpenRouter",
      "Provider": "OpenRouter",
      "Url": "https://openrouter.ai/api/v1",
      "ModelName": "openai/gpt-4-turbo",
      "ApiKey": "your-openrouter-key"
    }
  }
}
```

The role functionality stays the same, but now it uses a different provider.

## Docker Deployment

For Docker deployments, use environment variables:

```yaml
# docker-compose.yml
services:
  mnemosyne-core:
    environment:
      - MNEMOSYNE_OPENAI_API_KEY=${OPENAI_API_KEY}
      - MNEMOSYNE_ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - MNEMOSYNE_LLM_MASTER_API_KEY=${MASTER_API_KEY}
```

Create a `.env` file (git-ignored):
```bash
OPENAI_API_KEY=sk-your-openai-key-here
ANTHROPIC_API_KEY=sk-ant-your-anthropic-key-here
MASTER_API_KEY=sk-your-master-key-here
```

## Security Best Practices

1. **Never commit API keys** - Files containing keys are git-ignored
2. **Use environment variables in production** - More secure than files
3. **Rotate keys regularly** - Update keys periodically
4. **Limit key permissions** - Use least privilege principle
5. **Monitor usage** - Track API usage and costs

## Configuration Validation

The system validates configurations on startup. Check logs for validation errors:

```csharp
// Programmatic validation
var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
var validationResult = secureConfig.ValidateConfiguration();

if (validationResult.IsFailed)
{
    logger.LogError("Configuration validation failed: {Errors}", validationResult.Errors);
}
```

## Troubleshooting

### Common Issues

1. **"API key not found"**
   - Check environment variables are set correctly
   - Verify secrets.json exists and has correct format
   - Ensure case-sensitive naming matches

2. **"Model not configured"**
   - Verify model name exists in configuration
   - Check if model is enabled (`"Enabled": true`)
   - Validate JSON syntax in configuration files

3. **"Invalid URL"**
   - Ensure URLs include protocol (http:// or https://)
   - Check for typos in endpoint URLs
   - Verify local services are running (Ollama, LM Studio)

### Debug Configuration Loading

Enable debug logging to see configuration loading process:

```json
{
  "Logging": {
    "LogLevel": {
      "Mnemosyne.Core.Services.SecureConfigurationService": "Debug"
    }
  }
}
```

## File Security

The following files are automatically git-ignored:
- `appsettings.Local.json`
- `secrets.json`
- `*.local.json`
- `*.secrets.json`

Never commit these files to version control!