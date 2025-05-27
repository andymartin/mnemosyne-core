# Quick Start - LLM Configuration

To get Mnemosyne working with real AI responses, you need to configure an LLM provider with API keys.

## Option 1: OpenAI (Recommended for Quick Start)

1. **Get an OpenAI API key** from https://platform.openai.com/api-keys

2. **Create secrets file:**
   ```bash
   # In mnemosyne-core/src/MemoryCore/
   cp secrets.json.template secrets.json
   ```

3. **Add your API key to secrets.json:**
   ```json
   {
     "MasterApiKey": "sk-your-openai-key-here",
     "OpenAIApiKey": "sk-your-openai-key-here"
   }
   ```

4. **Create local configuration:**
   ```bash
   # In mnemosyne-core/src/MemoryCore/
   cp appsettings.Local.json.template appsettings.Local.json
   ```

5. **Start the system:**
   ```bash
   # From project root
   docker-compose up -d
   ```

6. **Test the chat interface:**
   - Open http://localhost:3000/
   - Send a message and get real AI responses!

## Option 2: OpenRouter (Multiple Models)

OpenRouter gives you access to many different models through one API.

1. **Get an OpenRouter API key** from https://openrouter.ai/keys

2. **Update secrets.json:**
   ```json
   {
     "MasterApiKey": "sk-or-your-openrouter-key-here"
   }
   ```

3. **Update appsettings.Local.json to use OpenRouter:**
   ```json
   {
     "LanguageModels": {
       "Master": {
         "Provider": "OpenRouter",
         "Url": "https://openrouter.ai/api/v1",
         "ModelName": "openai/gpt-4-turbo"
       }
     }
   }
   ```

## Option 3: Local Models (No API Key Needed)

For local models using Ollama:

1. **Install and start Ollama** with a model like llama2
2. **Update appsettings.Local.json:**
   ```json
   {
     "LanguageModels": {
       "Master": {
         "Provider": "Ollama",
         "Url": "http://localhost:11434",
         "ModelName": "llama2",
         "ApiKey": ""
       }
     }
   }
   ```

## Troubleshooting

- **"API key not found"**: Check that secrets.json exists and has the correct key
- **Connection errors**: Verify the API key is valid and has credits
- **"Model not configured"**: Ensure appsettings.Local.json has the correct model configuration

## What Changed

The system now uses **real LLM integration** instead of mock responses:
- ✅ Calls your configured LLM provider (OpenAI, OpenRouter, etc.)
- ✅ Builds context from conversation history and memories
- ✅ Returns actual AI-generated responses
- ✅ Tracks memory usage for evidence display

For detailed configuration options, see [README-Configuration.md](README-Configuration.md).