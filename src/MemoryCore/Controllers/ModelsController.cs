using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace Mnemosyne.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelsController : ControllerBase
    {
        private readonly LanguageModelOptions _languageModelOptions;

        public ModelsController(IOptions<LanguageModelOptions> languageModelOptions)
        {
            _languageModelOptions = languageModelOptions.Value;
        }

        [HttpGet]
        public ActionResult<IEnumerable<string>> GetModelConfigurations()
        {
            if (_languageModelOptions.Configurations == null)
            {
                return Ok(Enumerable.Empty<string>());
            }
            return Ok(_languageModelOptions.Configurations.Keys);
        }

        [HttpGet("assignments")]
        public ActionResult<Dictionary<LanguageModelType, string>> GetDefaultAssignments()
        {
            if (_languageModelOptions.DefaultAssignments == null)
            {
                return Ok(new Dictionary<LanguageModelType, string>());
            }
            return Ok(_languageModelOptions.DefaultAssignments);
        }
    }
}