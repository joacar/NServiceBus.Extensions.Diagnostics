﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.Diagnostics
{
    public class IncomingPhysicalMessageDiagnostics : Behavior<IIncomingPhysicalMessageContext>
    {
        private static readonly DiagnosticSource _diagnosticListener = new DiagnosticListener(ActivityNames.IncomingPhysicalMessage);

        public override async Task Invoke(
            IIncomingPhysicalMessageContext context,
            Func<Task> next)
        {
            var activity = StartActivity(context);

            try
            {
                await next().ConfigureAwait(false);
            }
            finally
            {
                StopActivity(activity, context);
            }
        }

        private static Activity StartActivity(IIncomingPhysicalMessageContext context)
        {
            var activity = new Activity(ActivityNames.IncomingPhysicalMessage);

            if (!context.MessageHeaders.TryGetValue(Headers.TraceParentHeaderName, out var requestId))
            {
                context.MessageHeaders.TryGetValue(Headers.RequestIdHeaderName, out requestId);
            }

            if (!string.IsNullOrEmpty(requestId))
            {
                activity.SetParentId(requestId);
                if (context.MessageHeaders.TryGetValue(Headers.TraceStateHeaderName, out var traceState))
                {
                    activity.TraceStateString = traceState;
                }

                if (context.MessageHeaders.TryGetValue(Headers.CorrelationContextHeaderName, out var correlationContext))
                {
                    var baggage = correlationContext.Split(',');
                    if (baggage.Length > 0)
                    {
                        foreach (var item in baggage)
                        {
                            if (NameValueHeaderValue.TryParse(item, out var baggageItem))
                            {
                                activity.AddBaggage(baggageItem.Name, HttpUtility.UrlDecode(baggageItem.Value));
                            }
                        }
                    }
                }

                foreach (var header in context.MessageHeaders.Where(kvp => kvp.Key.StartsWith("NServiceBus")))
                {
                    activity.AddTag(header.Key.Replace("NServiceBus.", ""), header.Value);
                }
            }

            _diagnosticListener.OnActivityImport(activity, context);

            if (_diagnosticListener.IsEnabled("Start", context))
            {
                _diagnosticListener.StartActivity(activity, context);
            }
            else
            {
                activity.Start();
            }

            return activity;
        }

        private static void StopActivity(Activity activity, IIncomingPhysicalMessageContext context)
        {
            _diagnosticListener.StopActivity(activity, context);
        }
    }
}