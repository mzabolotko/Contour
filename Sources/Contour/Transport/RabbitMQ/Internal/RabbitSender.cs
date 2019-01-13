using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contour.Configuration;
using Contour.Filters;
using Contour.Helpers.CodeContracts;
using Contour.Receiving;
using Contour.Sending;
using Contour.Transport.RabbitMQ.Topology;
using Microsoft.Extensions.Logging;

namespace Contour.Transport.RabbitMQ.Internal
{
    /// <summary>
    /// Отправитель сообщений с помощью брокера <c>RabbitMQ</c>.
    /// </summary>
    internal class RabbitSender : AbstractSender
    {
        private readonly ILogger logger;
        private readonly RabbitBus bus;
        private readonly IConnectionPool<IRabbitConnection> connectionPool;
        private readonly ConcurrentQueue<IProducer> producers = new ConcurrentQueue<IProducer>();
        private readonly RabbitSenderOptions senderOptions;
        private IProducerSelector producerSelector;
        private IFaultTolerantProducer faultTolerantProducer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitSender"/> class. 
        /// </summary>
        /// <param name="bus">
        /// A reference to the bus containing the sender
        /// </param>
        /// <param name="configuration">
        /// Конфигурация отправителя сообщений.
        /// </param>
        /// <param name="connectionPool">
        /// A bus connection pool
        /// </param>
        /// <param name="filters">
        /// Фильтры сообщений.
        /// </param>
        public RabbitSender(RabbitBus bus, ISenderConfiguration configuration, IConnectionPool<IRabbitConnection> connectionPool, IEnumerable<IMessageExchangeFilter> filters, ILoggerFactory loggerFactory)
            : base(bus.Endpoint, configuration, filters, loggerFactory)
        {
            this.bus = bus;
            this.connectionPool = connectionPool;
            this.senderOptions = (RabbitSenderOptions)this.Configuration.Options;
            this.logger = loggerFactory.CreateLogger($"{this.GetType().FullName}({this.bus.Endpoint}, {this.Configuration.Label})");
        }

        /// <summary>
        /// Если <c>true</c> - запущен, иначе <c>false</c>.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Если <c>true</c> - отправитель работает без сбоев, иначе <c>false</c>.
        /// </summary>
        public override bool IsHealthy => this.IsStarted;

        /// <summary>
        /// Освобождает занятые ресурсы. И останавливает отправителя.
        /// </summary>
        public override void Dispose()
        {
            this.logger.LogTrace("Disposing sender of [{Label}]", this.Configuration.Label);
            this.Stop();
        }

        /// <summary>
        /// Запускает отправителя.
        /// </summary>
        public override void Start()
        {
            if (this.IsStarted)
            {
                return;
            }

            this.logger.LogTrace("Starting sender of [{Label}]", this.Configuration.Label);

            this.StartProducers();
            this.IsStarted = true;
        }

        /// <summary>
        /// Останавливает отправителя.
        /// </summary>
        public override void Stop()
        {
            if (!this.IsStarted)
            {
                return;
            }

            this.logger.LogTrace("Stopping sender of [{Label}]", this.Configuration.Label);

            this.StopProducers();
            this.faultTolerantProducer.Dispose();

            this.IsStarted = false;
        }

        /// <summary>
        /// Выполняет отправку сообщения.
        /// </summary>
        /// <param name="exchange">Информация об отправке.</param>
        /// <returns>Задача ожидания отправки сообщения.</returns>
        protected override Task<MessageExchange> InternalSend(MessageExchange exchange)
        {
            return this.faultTolerantProducer.Send(exchange);
        }

        /// <summary>
        /// Starts the producers
        /// </summary>
        private void StartProducers()
        {
            this.logger.LogTrace("Starting producers for sender of [{Label}]", this.Configuration.Label);
            this.Configure();

            foreach (var producer in this.producers)
            {
                producer.Start();
            }
        }

        private void Configure()
        {
            this.BuildProducers();
            var builder = this.senderOptions.GetProducerSelectorBuilder();

            this.producerSelector = builder.Build(this.producers, this.loggerFactory);
            
            var sendAttempts = this.senderOptions.GetFailoverAttempts() ?? 1;
            var maxRetryDelay = this.senderOptions.GetMaxRetryDelay().GetValueOrDefault();
            var resetDelay = this.senderOptions.GetInactivityResetDelay().GetValueOrDefault();

            this.faultTolerantProducer = new FaultTolerantProducer(this.producerSelector, sendAttempts, maxRetryDelay, resetDelay, this.loggerFactory.CreateLogger<FaultTolerantProducer>());
        }

        /// <summary>
        /// Stops the producers
        /// </summary>
        private void StopProducers()
        {
            this.logger.LogTrace("Stopping producers for sender of [{Label}]", this.Configuration.Label);

            IProducer producer;
            while (!this.producers.IsEmpty && this.producers.TryDequeue(out producer))
            {
                try
                {
                    producer.Stop();
                    producer.Dispose();
                    this.logger.LogTrace("Producer stopped successfully");
                }
                catch (Exception ex)
                {
                    this.logger.LogError(
                        ex,
                        "Failed to stop producer [{Producer}] in sender of [{Label}] due to {Message}", producer, this.Configuration.Label, ex.Message);
                }
            }
        }

        /// <summary>
        /// Builds a set of producers constructing one producer for each URL in the connection string
        /// </summary>
        private void BuildProducers()
        {
            this.logger.LogTrace(
                "Building producers of [{Label}]: [{Urls}]", this.Configuration.Label, string.Join(",", this.senderOptions.RabbitConnectionString.Select(url => url)));

            foreach (var url in this.senderOptions.RabbitConnectionString)
            {
                this.EnlistProducer(url);
            }
        }

        private IProducer EnlistProducer(string url)
        {
            this.logger.LogTrace("Enlisting a new producer of [{Label}] at URL=[{url}]...", this.Configuration.Label, url);
            var producer = this.BuildProducer(url);

            this.producers.Enqueue(producer);
            this.logger.LogTrace("A producer of [{Label}] at URL=[{BrokerUrl}] has been enlisted", producer.Label, producer.BrokerUrl);

            return producer;
        }

        private Producer BuildProducer(string url)
        {
            var reuseConnectionProperty = this.senderOptions.GetReuseConnection();
            var reuseConnection = reuseConnectionProperty.HasValue && reuseConnectionProperty.Value;

            var source = new CancellationTokenSource();
            var connection = this.connectionPool.Get(url, reuseConnection, source.Token);
            this.logger.LogTrace("Using connection [{Id}] at URL=[{Url}] to resolve a producer", connection.Id, url);

            using (var topologyBuilder = new TopologyBuilder(connection))
            {
                var builder = new RouteResolverBuilder(this.bus.Endpoint, topologyBuilder, this.Configuration);
                var routeResolverBuilderFunc = this.Configuration.Options.GetRouteResolverBuilder();

                Assumes.True(
                    routeResolverBuilderFunc.HasValue,
                    "RouteResolverBuilder must be set for [{0}]",
                    this.Configuration.Label);

                var routeResolver = routeResolverBuilderFunc.Value(builder);

                var producer = new Producer(
                    this.bus.Endpoint,
                    connection,
                    this.Configuration.Label,
                    routeResolver,
                    this.Configuration.Options.IsConfirmationRequired(),
                    this.loggerFactory);

                if (this.Configuration.RequiresCallback)
                {
                    var callbackConfiguration = this.CreateCallbackReceiverConfiguration(url);
                    var receiver = this.bus.RegisterReceiver(callbackConfiguration, true);

                    this.logger.LogTrace(
                        "A sender of [{Label}] requires a callback configuration; registering a receiver of [{ReceiverLabel}] with connection string [{ConnectionString}]", 
                        this.Configuration.Label, 
                        callbackConfiguration.Label, 
                        callbackConfiguration.Options.GetConnectionString());

                    this.logger.LogTrace(
                        "A new callback receiver of [{Label}] with connection string [{ConnectionString}] has been successfully registered, getting one of its listeners with URL=[{BrokerUrl}]...",
                        callbackConfiguration.Label,
                        callbackConfiguration.Options.GetConnectionString(),
                        producer.BrokerUrl);

                    var listener = receiver.GetListener(l => l.BrokerUrl == producer.BrokerUrl);

                    if (listener == null)
                    {
                        throw new BusConfigurationException(
                            $"Unable to find a suitable listener for receiver {receiver}");
                    }

                    this.logger.LogTrace(
                        "A listener at URL=[{BrokerUrl}] belonging to callback receiver of [{ReveiverLabel}] acquired",
                        listener.BrokerUrl,
                        callbackConfiguration.Label);
                    
                    listener.StopOnChannelShutdown = true;
                    producer.UseCallbackListener(listener);

                    this.logger.LogTrace(
                        "A producer of [{Label}] at URL=[{BrokerUrl}] has registered a callback listener successfully",
                        producer.Label,
                        producer.BrokerUrl);
                }

                producer.StopOnChannelShutdown = true;
                producer.Stopped += (sender, args) =>
                {
                    this.OnProducerStopped(url, sender, args);
                };

                return producer;
            }
        }

        private void OnProducerStopped(string url, object sender, ProducerStoppedEventArgs args)
        {
            if (args.Reason == OperationStopReason.Regular)
            {
                return;
            }

            this.logger.LogWarning(
                "Producer [{HashCode}] has been stopped and will be reenlisted",
                sender.GetHashCode());

            while (true)
            {
                IProducer delistedProducer;
                if (this.producers.TryDequeue(out delistedProducer))
                {
                    if (sender == delistedProducer)
                    {
                        this.logger.LogTrace(
                            "Producer [{HashCode}] has been delisted",
                            delistedProducer.GetHashCode());
                        break;
                    }

                    this.producers.Enqueue(delistedProducer);
                }
            }

            var newProducer = this.EnlistProducer(url);
            newProducer.Start();
        }

        /// <summary>
        /// Overrides the callback configuration connection string with <paramref name="url"/>
        /// since the callback configuration may contain a list of connection strings for sharding support.
        /// </summary>
        /// <param name="url">The connection string URL</param>
        /// <returns>The reply receiver configuration</returns>
        private ReceiverConfiguration CreateCallbackReceiverConfiguration(string url)
        {
            var callbackConfiguration = new ReceiverConfiguration(
                MessageLabel.Any,
                this.Configuration.CallbackConfiguration.Options);

            callbackConfiguration.WithConnectionString(url);
            return callbackConfiguration;
        }
    }
}
