﻿using System;
using System.IO;
using System.Reflection;
using System.Web.Hosting;
using Data.Repositories;
using Fr8.Infrastructure.Interfaces;
using Fr8.Infrastructure.Utilities.Configuration;
using Hub.Interfaces;
using Hub.Services;
using PlanDirectory.Interfaces;
using StructureMap;
using StructureMap.Configuration.DSL;

namespace PlanDirectory.Infrastructure
{
    public class PlanDirectoryBootStrapper
    {
        public class LiveMode : Registry
        {
            public LiveMode()
            {
                For<IFr8Account>().Use<Fr8Account>().Singleton();
                For<IAuthTokenManager>().Use<AuthTokenManager>().Singleton();
                For<IPlanTemplate>().Use<PlanTemplate>().Singleton();
                For<ISearchProvider>().Use<SearchProvider>();
                For<ITagGenerator>().Use<TagGenerator>().Singleton();
                For<IPageDefinition>().Use<PageDefinition>().Singleton();
                For<IPageDefinitionRepository>().Use<PageDefinitionRepository>().Singleton();
                For<IHubCommunicatorFactory>().Use(
                    x => new PlanDirectoryHubCommunicatorFactory(
                        ObjectFactory.GetInstance<IRestfulServiceClientFactory>(),
                        CloudConfigurationManager.GetSetting("HubApiBaseUrl"),
                        CloudConfigurationManager.GetSetting("PlanDirectorySecret")
                    )
                );
                var serverPath = GetServerPath();
                var planDirectoryUrl = new Uri(CloudConfigurationManager.GetSetting("PlanDirectoryUrl"));
                ConfigureManifestPageGenerator(planDirectoryUrl, serverPath);
                ConfigurePlanPageGenerator(planDirectoryUrl, serverPath);
            }

            private void ConfigurePlanPageGenerator(Uri planDirectoryUrl, string serverPath)
            {
                var templateGenerator = new TemplateGenerator(new Uri($"{planDirectoryUrl}category"), $"{serverPath}/category");
                For<IWebservicesPageGenerator>().Use<WebservicesPageGenerator>().Singleton().Ctor<ITemplateGenerator>().Is(templateGenerator);
            }

            private void ConfigureManifestPageGenerator(Uri planDirectoryUrl, string serverPath)
            {
                var templateGenerator = new TemplateGenerator(new Uri($"{planDirectoryUrl}manifestpages"), $"{serverPath}/manifestpages");
                For<IManifestPageGenerator>().Use<ManifestPageGenerator>().Singleton().Ctor<ITemplateGenerator>().Is(templateGenerator);
            }

            private static string GetServerPath()
            {
                var serverPath = HostingEnvironment.MapPath("~");
                if (serverPath == null)
                {
                    var uriPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
                    serverPath = new Uri(uriPath).LocalPath;
                }
                return serverPath;
            }
        }

        public static void LiveConfiguration(ConfigurationExpression configuration)
        {
            configuration.AddRegistry<LiveMode>();
        }
    }
}