﻿using System;
using System.Linq;
using Data.Infrastructure;
using Data.Interfaces;
using Core.Managers;
using Core.Services;
using Core.StructureMap;
using Moq;
using NUnit.Framework;
using StructureMap;
using Utilities;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;

namespace DockyardTest.Managers
{
    [TestFixture]
    public class CommunicationManagerTest : BaseTest
    {
        [SetUp]
        public void Setup()
        {
            StructureMapBootStrapper.ConfigureDependencies(StructureMapBootStrapper.DependencyType.TEST);
        }

       
    }
}
