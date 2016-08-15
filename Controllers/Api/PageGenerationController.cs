﻿using System.Threading.Tasks;
using System.Web.Http;
using Data.States;
using Fr8.Infrastructure.Data.DataTransferObjects.PlanDirectory;
using Hub.Enums;
using Hub.Infrastructure;
using Hub.Interfaces;
using Hub.Managers;
using HubWeb.Infrastructure_PD;
using StructureMap;

namespace HubWeb.Controllers.Api
{
    public class PageGenerationController : ApiController
    {
        private readonly IManifestPageGenerator _manifestPageGenerator;
        private readonly IPlanTemplateDetailsGenerator _planTemplateDetailsGenerator;
        private readonly ISearchProvider _searchProvider;

        private readonly IPagesCheckUtility _pagesCheckUtility;

        public PageGenerationController(IManifestPageGenerator manifestPageGenerator,
                                        IPlanTemplateDetailsGenerator planTemplateDetailsGenerator,
                                        ISearchProvider searchProvider,
                                        IPagesCheckUtility pagesCheckUtility)
        {
            _manifestPageGenerator = manifestPageGenerator;
            _planTemplateDetailsGenerator = planTemplateDetailsGenerator;
            _searchProvider = searchProvider;
            _pagesCheckUtility = pagesCheckUtility;
        }

        [HttpPost]
        [ActionName("generate_manifest_page")]
        public async Task<IHttpActionResult> GenerateManifestPage([FromBody] string manifestName)
        {
            return Ok(await _manifestPageGenerator.Generate(manifestName, GenerateMode.GenerateIfNotExists));
        }

        [HttpGet]
        [ActionName("generate_pages")]
        [DockyardAuthorize(Roles = Roles.Admin)]
        public async Task<IHttpActionResult> GeneratePages()
        {
            var count = await _pagesCheckUtility.CheckPlanTempletesPages();

            return Ok($"it works. Number of templates {count}");
        }
    }
}
