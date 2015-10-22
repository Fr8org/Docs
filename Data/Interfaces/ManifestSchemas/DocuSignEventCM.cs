﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces.ManifestSchemas
{
    public class DocuSignEventCM : MTManifest
    {

        public string Object { get; set; }
        public string Status { get; set; }
        public string EventId { get; set; }
        public string EnvelopeId { get; set; }
        public string RecepientId { get; set; }

        public DocuSignEventCM()
              : base(Constants.MT.DocuSignEvent)
        {

        }
    }
}
