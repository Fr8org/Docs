﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Data.Entities;
using Data.Repositories;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Utilities.Configuration;
using Fr8.TerminalBase.Interfaces;
using Microsoft.AspNet.Identity;
using StructureMap;
using Hub.Infrastructure;
using Hub.Interfaces;
using PlanDirectory.Infrastructure;
using PlanDirectory.Interfaces;

namespace PlanDirectory.Controllers.Api
{
    [RoutePrefix("plan_templates")]
    public class PlanTemplatesController : ApiController
    {
        private readonly IHubCommunicator _hubCommunicator;
        private readonly IPlanTemplate _planTemplate;
        private readonly ISearchProvider _searchProvider;
        private readonly ITagGenerator _tagGenerator;
        private readonly IPageDefinition _pageDefinition;
        private readonly IPageGenerator _pageGenerator;

        public PlanTemplatesController()
        {
            _hubCommunicator = ObjectFactory.GetInstance<IHubCommunicator>();
            _hubCommunicator.Authorize(User.Identity.GetUserId());

            _planTemplate = ObjectFactory.GetInstance<IPlanTemplate>();
            _searchProvider = ObjectFactory.GetInstance<ISearchProvider>();
            _tagGenerator = ObjectFactory.GetInstance<ITagGenerator>();
            _pageGenerator = ObjectFactory.GetInstance<IPageGenerator>();
            _pageDefinition = ObjectFactory.GetInstance<IPageDefinition>();
        }

        [HttpPost]
        [Fr8ApiAuthorize]
        [PlanDirectoryHMACAuthenticate]
        public Task<IHttpActionResult> Post(PublishPlanTemplateDTO dto)
        {
            return ExceptionWrapper(async () =>
            {
                var fr8AccountId = User.Identity.GetUserId();

                var planTemplateCM = await _planTemplate.CreateOrUpdate(fr8AccountId, dto);

                var storage = await _tagGenerator.GetTags(planTemplateCM, fr8AccountId);

                await _searchProvider.CreateOrUpdate(planTemplateCM);

                var pageDefinitions = new List<PageDefinitionDO>();
                foreach (var tag in storage.WebServiceTemplateTags)
                {
                    var pd = new PageDefinitionDO()
                    {
                        Title = tag.Title,
                        Tags = tag.TagsWithIcons.Select(x => x.Key),
                        Type = "WebService"
                    };
                    pageDefinitions.Add(pd);
                }

                await _pageGenerator.Generate(storage, planTemplateCM, pageDefinitions, fr8AccountId);

                return Ok();
            });
        }

        [HttpDelete]
        [Fr8ApiAuthorize]
        [PlanDirectoryHMACAuthenticate]
        public Task<IHttpActionResult> Delete(Guid id)
        {
            return ExceptionWrapper(async () =>
            {
                var fr8AccountId = User.Identity.GetUserId();
                var planTemplateCM = await _planTemplate.Get(fr8AccountId, id);

                if (planTemplateCM != null)
                {
                    await _planTemplate.Remove(fr8AccountId, id);
                    await _searchProvider.Remove(id);
                }

                return Ok();
            });
        }

        [HttpGet]
        [Fr8ApiAuthorize]
        [PlanDirectoryHMACAuthenticate]
        public async Task<IHttpActionResult> Get(Guid id)
        {
            var fr8AccountId = User.Identity.GetUserId();
            var planTemplateDTO = await _planTemplate.Get(fr8AccountId, id);

            return Ok(planTemplateDTO);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Search(
            string text, int? pageStart = null, int? pageSize = null)
        {
            var searchRequest = new SearchRequestDTO()
            {
                Text = text,
                PageStart = pageStart.GetValueOrDefault(),
                PageSize = pageSize.GetValueOrDefault()
            };

            var searchResult = await _searchProvider.Search(searchRequest);

            return Ok(searchResult);
        }

        [HttpPost]
        [Fr8ApiAuthorize]
        [PlanDirectoryHMACAuthenticate]
        public Task<IHttpActionResult> CreatePlan(Guid id)
        {
            return ExceptionWrapper(async () =>
            {
                var fr8AccountId = User.Identity.GetUserId();
                var planTemplateDTO = await _planTemplate.Get(fr8AccountId, id);

                if (planTemplateDTO == null)
                {
                    throw new ApplicationException("Unable to find PlanTemplate in MT-database.");
                }

                var plan = await _hubCommunicator.LoadPlan(planTemplateDTO.PlanContents);

                return Ok(
                    new
                    {
                        RedirectUrl = CloudConfigurationManager.GetSetting("HubApiBaseUrl").Replace("/api/v1/", "")
                            + "/dashboard/plans/" + plan.Id.ToString() + "/builder?viewMode=plan"
                    }
                );
            });
        }

        // Added for PD <-> Hub debugging purposes only, to be removed in future.
        private Task<IHttpActionResult> ExceptionWrapper(Func<Task<IHttpActionResult>> handler)
        {
            try
            {
                return handler();
            }
            catch (Exception ex)
            {
                var sb = new System.Text.StringBuilder();

                while (ex != null)
                {
                    sb.AppendLine(ex.Message);
                    sb.AppendLine(ex.StackTrace);

                    ex = ex.InnerException;
                }

                return Task.FromResult<IHttpActionResult>(Ok(new { exception = sb.ToString() }));
            }
        }
    }
}