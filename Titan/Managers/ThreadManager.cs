﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Util;
using Serilog.Core;
using Titan.Account;
using Titan.Logging;
using Titan.Meta;
using Titan.Util;

namespace Titan.Managers
{
    public class ThreadManager
    {

        private Logger _log = LogCreator.Create();

        private Dictionary<TitanAccount, Task> _taskDic = new Dictionary<TitanAccount, Task>();

        private int _count; // Amount of accounts that successfully reported or commended

        public void StartReport(TitanAccount account, ReportInfo info)
        {
            if (_taskDic.ContainsKey(account))
            {
                _log.Warning("Account is already reporting / commending / idling. Aborting forcefully!");
                
                FinishBotting(account);
            }
            
            account.FeedReportInfo(info);

            _count = 0;
            
            _log.Debug("Starting reporting thread for {Target} in {Match} using account {Account}.",
                info.SteamID, info.MatchID, account.JsonAccount.Username);
            
            _taskDic.Add(account, Task.Run(() =>
            {
                var timedOut = false;

                try
                {
                    account.StartTick = DateTime.Now.Ticks;

                    // Timeout on Sentry Account: 3min (so the user has enough time to input the 2FA code), else 60sec.
                    var result = ((Func<Result>) account.Start).RunUntil(account.JsonAccount.Sentry 
                        ? TimeSpan.FromMinutes(3)
                        : TimeSpan.FromSeconds(60));

                    switch(result)
                    {
                        case Result.Success:
                            _count++;

                            if (account.IsLast)
                            {
                                _log.Information("SUCCESS! Titan has successfully sent {Amount} reports to {Target}.",
                                    _count, account._reportInfo.SteamID.ConvertToUInt64());
                                
                                Titan.Instance.UIManager.SendNotification(
                                    "Titan", _count + " reports have been successfully sent!"
                                );
                                
                                account.IsLast = false;
                            }
                            break;
                        case Result.AlreadyLoggedInSomewhereElse:
                            _log.Error("Could not report with account {Account}. The account is " +
                                       "already logged in somewhere else.", account.JsonAccount.Username);
                            break;
                        case Result.AccountBanned:
                            _log.Warning("Account {Account} has VAC or game bans on record. The report may " +
                                         "have not been submitted.");
                            _count++;
                            break;
                        case Result.NoMatches:
                            _log.Error("Could not receive match information for {Account}: User is not in live match.",
                                       account._liveGameInfo.SteamID.ConvertToUInt64());
                            break;
                        case Result.TimedOut:
                            _log.Error("Processing thread for {Account} has timed out.");
                            break;
                        case Result.SentryRequired:
                            _log.Error("The account has 2FA enabled. Please set {sentry} to {true} " +
                                       "in the accounts.json file.", "sentry", true);
                            break;
                        case Result.RateLimit:
                            _log.Error("The Steam Rate Limit has been reached. Please try again in a " +
                                       "few minutes.");
                            break;
                    }
                }
                catch (TimeoutException)
                {
                    var timeSpent = new DateTime(DateTime.Now.Ticks).Subtract(new DateTime(account.StartTick));

                    _log.Error("Connection to account {Account} timed out. It was not possible to " +
                               "report the target after {Timespan} seconds.", account.JsonAccount.Username, timeSpent.Seconds);
                    timedOut = true;
                }
                finally
                {
                    if(timedOut)
                    {
                        account.Stop();
                    }

                    _taskDic.Remove(account);
                }
            }));
        }

        public void StartCommend(TitanAccount account, CommendInfo info)
        {
            if (_taskDic.ContainsKey(account))
            {
                _log.Warning("Account is already reporting / commending / idling. Aborting forcefully!");
                
                FinishBotting(account);
            }
            
            account.FeedCommendInfo(info);

            _count = 0;
            
            _log.Debug("Starting commending thread for {Target} using account {Account}.",
                info.SteamID, account.JsonAccount.Username);
            
            _taskDic.Add(account, Task.Run(() =>
            {
                var timedOut = false;

                try
                {
                    account.StartTick = DateTime.Now.Ticks;

                    // Timeout on Sentry Account: 3min (so the user has enough time to input the 2FA code), else 60sec.
                    var result = ((Func<Result>) account.Start).RunUntil(account.JsonAccount.Sentry 
                        ? TimeSpan.FromMinutes(3)
                        : TimeSpan.FromSeconds(60));

                    switch(result)
                    {
                        case Result.Success:
                            _count++;
                            
                            if (account.IsLast)
                            {
                                _log.Information("SUCCESS! Titan has successfully sent {Amount} commends to {Target}.",
                                    _count, account._reportInfo.SteamID.ConvertToUInt64());
                                
                                Titan.Instance.UIManager.SendNotification(
                                    "Titan", _count + " commends have been successfully sent!"
                                );
                                
                                account.IsLast = false;
                            }
                            break;
                        case Result.AlreadyLoggedInSomewhereElse:
                            _log.Error("Could not commend with account {Account}. The account is " +
                                       "already logged in somewhere else.", account.JsonAccount.Username);
                            break;
                        case Result.AccountBanned:
                            _log.Warning("Account {Account} has VAC or game bans on record. The report may " +
                                         "have not been submitted.");
                            _count++;
                            break;
                        case Result.TimedOut:
                            _log.Error("Processing thread for {Account} has timed out.");
                            break;
                        case Result.SentryRequired:
                            _log.Error("The account has 2FA enabled. Please set {sentry} to {true} " +
                                       "in the accounts.json file.", "sentry", true);
                            break;
                        case Result.RateLimit:
                            _log.Error("The Steam Rate Limit has been reached. Please try again in a " +
                                       "few minutes.");
                            break;
                    }
                }
                catch (TimeoutException)
                {
                    var timeSpent = new DateTime(DateTime.Now.Ticks).Subtract(new DateTime(account.StartTick));

                    _log.Error("Connection to account {Account} timed out. It was not possible to " +
                               "commend the target after {Timespan} seconds.", account.JsonAccount.Username, timeSpent.Seconds);
                    timedOut = true;
                }
                finally
                {
                    if(timedOut)
                    {
                        account.Stop();
                    }

                    _taskDic.Remove(account);
                }
            }));
        }

        public void StartMatchResolving(TitanAccount account, LiveGameInfo info)
        {
            if (_taskDic.ContainsKey(account))
            {
                _log.Warning("Account is already reporting / commending / idling. Aborting forcefully!");
                
                FinishBotting(account);
            }
            
            account.FeedLiveGameInfo(info);
            
            _log.Debug("Starting Match ID resolving thread for {Target} using account {Account}.",
                info.SteamID, account.JsonAccount.Username);
            
            _taskDic.Add(account, Task.Run(() =>
            {
                var timedOut = false;

                try
                {
                    account.StartTick = DateTime.Now.Ticks;

                    // Timeout on Sentry Account: 3min (so the user has enough time to input the 2FA code), else 60sec.
                    var result = ((Func<Result>) account.Start).RunUntil(account.JsonAccount.Sentry 
                        ? TimeSpan.FromMinutes(3)
                        : TimeSpan.FromSeconds(60));

                    switch(result)
                    {
                        case Result.Success:
                            _count++;
                            break;
                        case Result.AlreadyLoggedInSomewhereElse:
                            _log.Error("Could not resolve Match ID with account {Account}. The account is " +
                                       "already logged in somewhere else.", account.JsonAccount.Username);
                            break;
                        case Result.TimedOut:
                            _log.Error("Processing thread for {Account} has timed out.");
                            break;
                        case Result.SentryRequired:
                            _log.Error("The account has 2FA enabled. Please set {sentry} to {true} " +
                                       "in the accounts.json file.", "sentry", true);
                            break;
                        case Result.RateLimit:
                            _log.Error("The Steam Rate Limit has been reached. Please try again in a " +
                                       "few minutes.");
                            break;
                    }
                }
                catch (TimeoutException)
                {
                    var timeSpent = new DateTime(DateTime.Now.Ticks).Subtract(new DateTime(account.StartTick));

                    _log.Error("Connection to account {Account} timed out. It was not possible to resolve the Match " +
                               "ID for the target after {Timespan} seconds.", account.JsonAccount.Username, timeSpent.Seconds);
                    timedOut = true;
                }
                finally
                {
                    if(timedOut)
                    {
                        account.Stop();
                    }

                    _taskDic.Remove(account);
                }
            }));
            
            _count = 0;
        }

        public void FinishBotting()
        {
            foreach (var pair in _taskDic)
            {
                if (pair.Key.IsRunning || !pair.Value.IsCompleted)
                {
                    pair.Key.Stop();
                    
                    _log.Warning("Forcefully finished botting of account {account}.", pair.Key.JsonAccount.Username);
                }

                _taskDic.Remove(pair.Key);
            }
        }

        public void FinishBotting(TitanAccount acc)
        {
            if(acc.IsRunning)
            {
                acc.Stop();
            }
            
            if(_taskDic.ContainsKey(acc))
            {
                _taskDic.Remove(acc);
            }
        }

    }
}