using System.Collections.Generic;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Models.Client
{
    public class ModelConfigurationDto
    {
        public Dictionary<LanguageModelType, string> AssignedModels { get; set; } = new();
    }
}