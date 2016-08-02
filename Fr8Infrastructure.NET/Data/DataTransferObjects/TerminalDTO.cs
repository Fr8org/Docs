﻿using Newtonsoft.Json;

namespace Fr8.Infrastructure.Data.DataTransferObjects
{
    public class TerminalDTO 
    {
        public TerminalDTO()
        {
            AuthenticationType = States.AuthenticationType.None;
        }

        [JsonProperty("id")]
        public string PublicIdentifier { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("label")]
        public string Label { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("terminalStatus")]
        public int TerminalStatus { get; set; }
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("authenticationType")]
        public int AuthenticationType { get; set; }
    }
}