using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class DefaultProducerSelectorBuilder : IProducerSelectorBuilder
    {
        public IProducerSelector Build(IEnumerable<IProducer> items, ILoggerFactory loggerFactory)
        {
            return new RoundRobinSelector(items, loggerFactory.CreateLogger<RoundRobinSelector>());
        }
    }
}