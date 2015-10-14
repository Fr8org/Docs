﻿using System;
using System.Linq;
using System.Web.Http.Results;
using NUnit.Framework;
using StructureMap;
using Core.Services;
using Data.Entities;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Data.States;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;
using Web.Controllers;
using Web.ViewModels;
using DockyardTest.Controllers.Api;

namespace DockyardTest.Controllers
{
    [TestFixture]
    [Category("ActionListController")]
    public class ActionListControllerTest : ApiControllerTestBase
    {
        private ProcessNodeTemplateDO _curProcessNodeTemplate;
        private ActionListController _actionListController;

        public override void SetUp()
        {
            base.SetUp();
            // DO-1214
            //InitializeActionList();
            _actionListController = CreateController<ActionListController>();
        }
        // DO-1214
//        [Test]
//        public void ActionListController_CanGetByProcessNodeTemplateId()
//        {
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//
//
//                var actionResult = _actionListController.GetByProcessNodeTemplateId(
//                    _curProcessNodeTemplate.Id, ActionListType.Immediate);
//
//                var okResult = actionResult as OkNegotiatedContentResult<ActionListDTO>;
//
//                Assert.IsNotNull(okResult);
//                Assert.IsNotNull(okResult.Content);
//                Assert.AreEqual(okResult.Content.Id, _curActionList.Id);
//            }
//        }
//
//        #region Private methods
//        private void InitializeActionList()
//        {
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                //Add a template
//                var curRoute = FixtureData.TestRoute1();
//                uow.RouteRepository.Add(curRoute);
//                uow.SaveChanges();
//
//                _curProcessNodeTemplate = FixtureData.TestProcessNodeTemplateDO1();
//                _curProcessNodeTemplate.ParentTemplateId = curRoute.Id;
//                uow.ProcessNodeTemplateRepository.Add(_curProcessNodeTemplate);
//                uow.SaveChanges();
//
//                /*_curProcessNodeTemplate = FixtureData.TestProcessNodeTemplateDO1();
//                uow.ProcessNodeTemplateRepository.Add(_curProcessNodeTemplate);
//                uow.SaveChanges();*/
//
//                _curActionList = FixtureData.TestActionList();
//                _curActionList.ActionListType = ActionListType.Immediate;
//                _curActionList.CurrentActivity = null;
//                _curActionList.ProcessNodeTemplateID = _curProcessNodeTemplate.Id;
//
//                uow.ActionListRepository.Add(_curActionList);
//                uow.SaveChanges();
//            }
//        }
//
//        #endregion
    }

}
