﻿using System.Net;
using System.Web.Http;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Interfaces;
using StructureMap;
using Hub.Infrastructure;
using HubWeb.Infrastructure_HubWeb;
using Data.Infrastructure.StructureMap;
using Fr8.Infrastructure.Data.Constants;
using Swashbuckle.Swagger.Annotations;

namespace HubWeb.Controllers
{
    public class NotificationsController : Fr8BaseApiController
    {
        private IPusherNotifier _pusherNotifier;
        private readonly ISecurityServices _security;

        public NotificationsController()
        {
            _pusherNotifier = ObjectFactory.GetInstance<IPusherNotifier>();
        }
        /// <summary>
        /// Post specified notification message to the activity feed of current user
        /// </summary>
        /// <remarks>Fr8 authentication headers must be provided</remarks>
        /// <param name="notificationMessage">Message to post</param>
        [HttpPost]
        [Fr8TerminalAuthentication]
        [Fr8ApiAuthorize]
        [SwaggerResponse(HttpStatusCode.OK, "Message was successfully posted")]
        [SwaggerResponse(HttpStatusCode.Unauthorized, "Unauthorized request")]
        [SwaggerResponseRemoveDefaults]
        public IHttpActionResult Post(NotificationMessageDTO notificationMessage)
        {
            string userId;

            if (IsThisTerminalCall())
            {
                var user = GetUserTerminalOperatesOn();
                userId = user?.Id;
            }
            else
            {
                userId = _security.GetCurrentUser();
            }

            notificationMessage.NotificationType = NotificationType.TerminalEvent;

            _pusherNotifier.NotifyUser(notificationMessage, userId);
            return Ok();
        }
    }
}