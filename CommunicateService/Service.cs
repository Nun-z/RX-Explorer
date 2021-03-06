﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace CommunicateService
{
    public sealed class Service : IBackgroundTask
    {
        private BackgroundTaskDeferral Deferral;
        private static readonly ConcurrentDictionary<AppServiceConnection, AppServiceConnection> PairedConnections = new ConcurrentDictionary<AppServiceConnection, AppServiceConnection>();
        private static readonly ConcurrentQueue<AppServiceConnection> ClientWaitingQueue = new ConcurrentQueue<AppServiceConnection>();
        private static readonly ConcurrentQueue<AppServiceConnection> ServerWaitingrQueue = new ConcurrentQueue<AppServiceConnection>();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            AppServiceConnection IncomeConnection = (taskInstance.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;

            IncomeConnection.RequestReceived += Connection_RequestReceived;

            AppServiceResponse Response = await IncomeConnection.SendMessageAsync(new ValueSet { { "ExecuteType", "Identity" } });

            if (Response.Status == AppServiceResponseStatus.Success)
            {
                if (Response.Message.TryGetValue("Identity", out object Identity))
                {
                    switch (Convert.ToString(Identity))
                    {
                        case "FullTrustProcess":
                            {
                                if (ClientWaitingQueue.TryDequeue(out AppServiceConnection ClientConnection))
                                {
                                    PairedConnections.TryAdd(ClientConnection, IncomeConnection);
                                }
                                else
                                {
                                    ServerWaitingrQueue.Enqueue(IncomeConnection);
                                }

                                break;
                            }
                        case "UWP":
                            {
                                if (ServerWaitingrQueue.TryDequeue(out AppServiceConnection ServerConnection))
                                {
                                    PairedConnections.TryAdd(IncomeConnection, ServerConnection);
                                }
                                else
                                {
                                    ClientWaitingQueue.Enqueue(IncomeConnection);
                                }

                                break;
                            }
                    }
                }
            }
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                AppServiceConnection ServerConnection = null;

                if (SpinWait.SpinUntil(() => PairedConnections.ContainsKey(sender), 3000))
                {
                    ServerConnection = PairedConnections[sender];
                }

                if (ServerConnection != null)
                {
                    AppServiceResponse ServerRespose = await ServerConnection.SendMessageAsync(args.Request.Message);

                    if (ServerRespose.Status == AppServiceResponseStatus.Success)
                    {
                        await args.Request.SendResponseAsync(ServerRespose.Message);
                    }
                    else
                    {
                        await args.Request.SendResponseAsync(new ValueSet { { "Error", "Can't not send message to server" } });
                    }
                }
                else
                {
                    ValueSet Value = new ValueSet
                    {
                        { "Error", "Failed to wait a server connection within the specified time" }
                    };

                    await args.Request.SendResponseAsync(Value);
                }
            }
            catch
            {
                ValueSet Value = new ValueSet
                {
                    { "Error", "Some exceptions were threw while transmitting the message" }
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            try
            {
                if ((sender.TriggerDetails as AppServiceTriggerDetails)?.AppServiceConnection is AppServiceConnection DisConnection)
                {
                    try
                    {
                        DisConnection.RequestReceived -= Connection_RequestReceived;

                        if (PairedConnections.TryRemove(DisConnection, out AppServiceConnection ServerConnection))
                        {
                            Task.WaitAny(ServerConnection.SendMessageAsync(new ValueSet { { "ExecuteType", "Execute_Exit" } }).AsTask(), Task.Delay(2000));
                        }
                        else if (PairedConnections.FirstOrDefault((Con) => Con.Value == DisConnection).Key is AppServiceConnection ClientConnection)
                        {
                            if (PairedConnections.TryRemove(ClientConnection, out _))
                            {
                                Task.WaitAny(ClientConnection.SendMessageAsync(new ValueSet { { "ExecuteType", "FullTrustProcessExited" } }).AsTask(), Task.Delay(2000));
                                ClientConnection.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        DisConnection.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error was threw in CommunicateService: {ex.Message}");
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
