using Contour.Receiving;
using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class RabbitCallbackReceiver : RabbitReceiver
    {
        public RabbitCallbackReceiver(RabbitBus bus, IReceiverConfiguration configuration, IConnectionPool<IRabbitConnection> connectionPool, ILoggerFactory loggerFactory)
            : base(bus, configuration, connectionPool, loggerFactory)
        {
        }

        protected override void OnListenerCreated(IListener listener)
        {
            // Suppress listener registrations for the callback receiver
        }
    }
}