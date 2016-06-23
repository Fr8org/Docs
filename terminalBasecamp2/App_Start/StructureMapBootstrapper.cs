﻿using Fr8.TerminalBase.Interfaces;
using Fr8.TerminalBase.Services;
using StructureMap;
using terminalBasecamp2.Infrastructure;

namespace terminalBasecamp2
{
    public class StructureMapBootstrapper
    {
        public static void LiveMode(ConfigurationExpression expression)
        {
            expression.For<IBasecampApiClient>().Use<BasecampApiClient>();
        }
    }
}