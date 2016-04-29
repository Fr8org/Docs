﻿using System;
using System.Threading.Tasks;
using Data.Entities;

namespace terminalSlack.Interfaces
{
    public interface ISlackEventManager : IDisposable
    {
        Task Subscribe(AuthorizationTokenDO token, Guid activityId);

        void Unsubscribe(Guid activityId);
    }
}
