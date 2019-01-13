using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class RabbitConnectionProvider : IConnectionProvider<IRabbitConnection>
    {
        private readonly ILogger<RabbitConnectionProvider> logger;
        private readonly IEndpoint endpoint;
        private readonly IBusContext context;
        private readonly ILoggerFactory loggerFactory;

        public RabbitConnectionProvider(IBusContext context, ILoggerFactory loggerFactory)
        {
            this.endpoint = context.Endpoint;
            this.context = context;
            this.loggerFactory = loggerFactory;
            this.logger = this.loggerFactory.CreateLogger<RabbitConnectionProvider>();
        }

        public IRabbitConnection Create(string connectionString)
        {
            this.logger.LogTrace(
                "Creating a new connection for endpoint [{Endpoint}] at [{ConnectionString}]", 
                this.endpoint, 
                connectionString);
            return new RabbitConnection(this.endpoint, connectionString, this.context, this.loggerFactory);
        }
    }
}
