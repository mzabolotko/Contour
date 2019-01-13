using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class RoundRobinSelector : IProducerSelector
    {
        private readonly object syncRoot = new object();
        private readonly IEnumerable<IProducer> producers;
        private readonly ILogger<RoundRobinSelector> logger;
        private IEnumerator<IProducer> enumerator;

        public RoundRobinSelector(IEnumerable<IProducer> items, ILogger<RoundRobinSelector> logger)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            this.producers = items;
            this.logger = logger;
            this.enumerator = this.producers.GetEnumerator();
        }

        public IProducer Next()
        {
            return this.NextInternal();
        }

        public IProducer Next(IMessage message)
        {
            return this.NextInternal();
        }

        private IProducer NextInternal()
        {
            lock (this.syncRoot)
            {
                var freshCycle = false;

                while (true)
                {
                    if (this.enumerator.MoveNext())
                    {
                        return this.enumerator.Current;
                    }

                    if (freshCycle)
                    {
                        throw new Exception("Unable to take the next producer because no available producers left");
                    }

                    this.logger.LogTrace("Starting the next round of producers' selection");
                    freshCycle = true;

                    this.enumerator = this.producers.GetEnumerator();
                }
            }
        }
    }
}