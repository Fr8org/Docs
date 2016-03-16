﻿using System.Runtime.Remoting;
using Data.Interfaces.Manifests;
using HealthMonitor.Utility;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace terminalGoogleTests.Integration
{
    /// <summary>
    /// Terminal Google Discover v1 test
    /// Mark test case class with [Explicit] attiribute.
    /// It prevents test case from running when CI is building the solution,
    /// but allows to trigger that class from HealthMonitor.
    /// </summary>
    [Explicit]
    public class Terminal_Discover_v1_Tests : BaseTerminalIntegrationTest
    {
        public override string TerminalName
        {
            get { return "terminalGoogle"; }
        }

        /// <summary>
        /// Validate correct crate-storage structure in initial configuration response.
        /// </summary>
        [Test, Category("Integration.terminalGoogle")]
        public async Task Terminal_Google_Discover()
        {
            var discoverUrl = GetTerminalDiscoverUrl();

            var googleTerminalDiscoveryResponse = await HttpGetAsync<StandardFr8TerminalCM>(discoverUrl);

            Assert.IsNotNull(googleTerminalDiscoveryResponse, "Terminal Google discovery did not happen.");
            Assert.IsNotNull(googleTerminalDiscoveryResponse.Activities, "Google terminal does not have actions.");
            Assert.AreEqual(3, googleTerminalDiscoveryResponse.Activities.Count, "Google terminal expected 3 actions.");
            Assert.AreEqual("terminalGoogle", googleTerminalDiscoveryResponse.Definition.Name);
            Assert.AreEqual(googleTerminalDiscoveryResponse.Activities.Any(a => a.Name == "Get_Google_Sheet_Data"), true, "Action Get_Google_Sheet_Data was not loaded");
            Assert.AreEqual(googleTerminalDiscoveryResponse.Activities.Any(a => a.Name == "Receive_Google_Form"), true, "Action Receive_Google_Form was not loaded");
            Assert.AreEqual(googleTerminalDiscoveryResponse.Activities.Any(a => a.Name == "Save_To_Google_Sheet"), true, "Action Save_To_Google_Sheet was not loaded");
        }
    }
}
