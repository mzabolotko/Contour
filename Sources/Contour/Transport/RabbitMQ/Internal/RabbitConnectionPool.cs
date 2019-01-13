using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class RabbitConnectionPool : ConnectionPool<IRabbitConnection>
    {
        public RabbitConnectionPool(IBusContext context, ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            this.Provider = new RabbitConnectionProvider(context, loggerFactory);
        }
    }
}