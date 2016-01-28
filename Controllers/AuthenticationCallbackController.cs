﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using StructureMap;
using Data.Entities;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Hub.Interfaces;
using Hub.Services;
using HubWeb.ViewModels;
using Data.Infrastructure;

namespace HubWeb.Controllers
{
    public class AuthenticationCallbackController : Controller
    {
        private readonly IActivity _activity;
        private readonly IAuthorization _authorization;
        private readonly ITerminal _terminal;

        public AuthenticationCallbackController()
        {
            _terminal = ObjectFactory.GetInstance<ITerminal>();
            _activity = ObjectFactory.GetInstance<IActivity>();
            _authorization = ObjectFactory.GetInstance<IAuthorization>();
        }

        [HttpGet]
        public async Task<ActionResult> ProcessSuccessfulOAuthResponse(
            string terminalName,
            string terminalVersion)
        {
            if (string.IsNullOrEmpty(terminalName) || string.IsNullOrEmpty(terminalVersion))
            {
                throw new ApplicationException("TerminalName or TerminalVersion is not specified.");
            }

            var requestQueryString = Request.Url.Query;
            if (!string.IsNullOrEmpty(requestQueryString) && requestQueryString[0] == '?')
            {
                requestQueryString = requestQueryString.Substring(1);
            }

            TerminalDO terminal = _terminal.GetAll().FirstOrDefault(x => x.Name == terminalName && x.Version == terminalVersion);

            if (terminal == null)
            {
                throw new ApplicationException("Could not find terminal.");
            }
            
            var externalAuthenticationDTO = new ExternalAuthenticationDTO()
            {
                RequestQueryString = requestQueryString
            };

            var response = await _authorization.GetOAuthToken(terminal, externalAuthenticationDTO);

            if (string.IsNullOrEmpty(response.Error))
            {
                return View(response);
            }
            else
            {
                EventManager.OAuthAuthenticationFailed(requestQueryString, response.Error);
                return View("Error", new AuthenticationErrorVM()
                {
                    Error = response.Error
                });
            }
        }
    }
}