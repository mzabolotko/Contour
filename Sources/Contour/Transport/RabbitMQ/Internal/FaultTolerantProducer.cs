using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class FaultTolerantProducer : IFaultTolerantProducer
    {
        private readonly IProducerSelector selector;
        private readonly int attempts;
        private readonly ILogger<FaultTolerantProducer> logger;
        private bool disposed;

        public FaultTolerantProducer(IProducerSelector selector, int maxAttempts, int maxRetryDelay, int inactivityResetDelay, ILogger<FaultTolerantProducer> logger)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            this.selector = selector;
            this.attempts = maxAttempts;
            this.logger = logger;
        }

        public IEnumerable<KeyValuePair<int, int>> Delays { get; } = new ConcurrentDictionary<int, int>();

        public Task<MessageExchange> Send(MessageExchange exchange)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(typeof(FaultTolerantProducer).Name);
            }

            var errors = new List<Exception>();

            for (var count = 0; count < this.attempts; count++)
            {
                this.logger.LogTrace("Attempt to send #{Count}", count);

                try
                {
                    var producer = this.selector.Next();
                    return this.TrySend(exchange, producer);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Attempt #{Count} to send a message has failed", count);
                    errors.Add(ex);
                }
            }

            throw new FailoverException($"Failed to send a message after {this.attempts} attempts", new AggregateException(errors))
            {
                Attempts = this.attempts
            };
        }

        private Task<MessageExchange> TrySend(MessageExchange exchange, IProducer producer)
        {
            if (exchange.IsRequest)
            {
                return producer.Request(exchange.Out, exchange.ExpectedResponseType)
                    .ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted)
                            {
                                exchange.Exception = t.Exception;
                            }
                            else
                            {
                                exchange.In = t.Result;
                            }

                            return exchange;
                        });
            }

            return producer.Publish(exchange.Out)
                .ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                        {
                            exchange.Exception = t.Exception;
                        }

                        return exchange;
                    });
        }
        
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }
    }
}