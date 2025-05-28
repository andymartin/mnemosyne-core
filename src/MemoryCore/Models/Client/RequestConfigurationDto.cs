using System.Collections.Generic;
using Mnemosyne.Core.Models;

namespace Mnemosyne.Core.Models.Client
{
    public class RequestConfigurationDto
    {
        public Dictionary<LanguageModelType, string> Selections { get; set; } = new();
        public string? PipelineId { get; set; }
    }
}