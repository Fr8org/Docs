﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Fr8.Infrastructure.Data.States;

namespace Fr8.Infrastructure.Data.DataTransferObjects
{
    public class ActivityTemplateDTO
    {
        public ActivityTemplateDTO()
        {
            Type = ActivityType.Standard;
        }

        //[JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("webService")]
        public WebServiceDTO WebService { get; set; }

        [JsonProperty("terminal")]
        public TerminalDTO Terminal { get; set; }

        [JsonProperty("tags")]
        public string Tags { get; set; }

        [JsonProperty("categories")]
        public ActivityCategoryDTO[] Categories { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ActivityCategory Category { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ActivityType Type { get; set; }

        [JsonProperty("minPaneWidth")]
        public int MinPaneWidth { get; set; }

        public bool NeedsAuthentication { get; set; }

        [JsonProperty("showDocumentation")]
        public ActivityResponseDTO ShowDocumentation { get; set; }
    }
}
