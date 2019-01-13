using Contour.Receiving;
using Microsoft.Extensions.Logging;
using System;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class DefaultFailedDeliveryStrategyBuilder : IFailedDeliveryStrategyBuilder
    {
        public IFailedDeliveryStrategy Build(ILoggerFactory loggerFactory)
        {
            Action<IFailedConsumingContext> failedDeliveryHandler =
            d =>
            {
                d.Forward("document.Contour.failed", d.BuildFaultMessage());
                d.Accept();
            };

            ILogger<LambdaFailedDeliveryStrategy> logger =
                loggerFactory.CreateLogger<LambdaFailedDeliveryStrategy>();

            return new LambdaFailedDeliveryStrategy(failedDeliveryHandler, logger);
        }
    }

}
