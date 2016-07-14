﻿using PlanDirectory.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StructureMap;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Interfaces;
using Fr8.Infrastructure.Utilities.Configuration;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.DataTransferObjects.PlanTemplates;

namespace PlanDirectory.Infrastructure
{
    public class TagGenerator : ITagGenerator
    {
        /// <summary>
        /// The result of this method is a a list of ActivityTemplateTag and WebServiceTemplateTag classes
        /// For a plan, that consists of activity named "A" of a webservice "Y"
        /// and of activity named "B" of a webservice "Z"
        /// the result would be:
        /// ActivityTemplateTag: A
        /// ActivityTemplateTag: B
        /// ActivityTemplateTag: A, B
        /// WebServiceTemplateTag: Y
        /// WebServiceTemplateTag: Z
        /// WebServiceTemplateTag: Y, Z
        /// </summary>

        public async Task<TemplateTagStorage> GetTags(PlanTemplateCM planTemplateCM, string fr8AccountId)
        {
            var result = new TemplateTagStorage();

            //requesting all activity templates
            var hmacService = ObjectFactory.GetInstance<IHMACService>();
            var client = ObjectFactory.GetInstance<IRestfulServiceClient>();

            var uri = new Uri(CloudConfigurationManager.GetSetting("HubApiBaseUrl") + "activitytemplates");
            var headers = await hmacService.GenerateHMACHeader(
                uri,
                "PlanDirectory",
                CloudConfigurationManager.GetSetting("PlanDirectorySecret"),
                fr8AccountId,
                null
            );

            var activityCategories = await client.GetAsync<IEnumerable<ActivityTemplateCategoryDTO>>(
               uri, headers: headers);

            var activityDict = activityCategories.SelectMany(a => a.Activities).ToDictionary(k => k.Id);

            //1. getting ids of used templates
            var planTemplateDTO = JsonConvert.DeserializeObject<PlanTemplateDTO>(planTemplateCM.PlanContents);
            if (planTemplateDTO.PlanNodeDescriptions == null || planTemplateDTO.PlanNodeDescriptions.Count == 0)
                return new TemplateTagStorage();

            var usedActivityTemplatesIds = planTemplateDTO.PlanNodeDescriptions.Select(a => a.ActivityDescription.ActivityTemplateId).Distinct().ToList();
            //2. getting used templates
            var usedActivityTemplates = usedActivityTemplatesIds.Intersect(activityDict.Keys)
                                     .Select(k => activityDict[k])
                                     .Distinct()
                                     .OrderBy(a => a.Name)
                                     .ToList();

            if (usedActivityTemplates.Count != usedActivityTemplatesIds.Count)
                throw new ApplicationException("Template references activity that is not registered in Hub");
            //3. adding tags for activity templates
            var activityTemplatesCombinations = GetCombinations<ActivityTemplateDTO>(usedActivityTemplates);
            activityTemplatesCombinations.ForEach(a => result.ActivityTemplateTags.Add(new ActivityTemplateTag(a)));

            //4. adding tags for webservices
            var usedWebServices = usedActivityTemplates.Select(a => a.WebService).Distinct().OrderBy(b => b.Name).ToList();
            var webServicesCombination = GetCombinations<WebServiceDTO>(usedWebServices);
            webServicesCombination.ForEach(a => result.WebServiceTemplateTags.Add(new WebServiceTemplateTag(a)));

            return result;
        }

        /// <summary>
        /// K-combination algorythm implementation. 
        /// For input: "A, B, C"
        /// would output:
        /// A,
        /// A,B,
        /// A,B,C,
        /// A,C,
        /// B,
        /// B,C,
        /// C,
        /// 
        /// might require optimisation if this chunk will ever become a bottleneck
        /// </summary>
        private List<List<T>> GetCombinations<T>(List<T> list)
        {
            var result = new List<List<T>>();
            double count = Math.Pow(2, list.Count);
            for (int i = 1; i <= count - 1; i++)
            {
                var row = new List<T>();
                string str = Convert.ToString(i, 2).PadLeft(list.Count, '0');
                for (int j = 0; j < str.Length; j++)
                {
                    if (str[j] == '1')
                    {
                        row.Add(list[j]);
                    }
                }
                result.Add(row);
            }
            return result;
        }
    }
}