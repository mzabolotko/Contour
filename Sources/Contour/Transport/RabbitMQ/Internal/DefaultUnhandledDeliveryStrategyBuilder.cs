using Contour.Receiving;
using Microsoft.Extensions.Logging;
using System;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class DefaultUnhandledDeliveryStrategyBuilder : IUnhandledDeliveryStrategyBuilder
    {
        public IUnhandledDeliveryStrategy Build(ILoggerFactory loggerFactory)
        {
            Action<IFaultedConsumingContext> unhandledDeliveryHandler =
            d =>
            {
                d.Forward("document.Contour.unhandled", d.BuildFaultMessage());
                d.Accept();
            };

            ILogger<LambdaUnhandledDeliveryStrategy> logger =
                loggerFactory.CreateLogger<LambdaUnhandledDeliveryStrategy>();

            return new LambdaUnhandledDeliveryStrategy(unhandledDeliveryHandler, logger);
        }
    }
}
