using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using INativeConnection = RabbitMQ.Client.IConnection;

namespace Contour.Transport.RabbitMQ.Internal
{
    internal class RabbitConnection : IRabbitConnection
    {
        private const int ConnectionTimeout = 3000;
        private const int OperationTimeout = 500;
        private readonly object syncRoot = new object();
        private readonly ILogger logger;
        private readonly IEndpoint endpoint;
        private readonly IBusContext busContext;
        private readonly ILoggerFactory loggerFactory;
        private readonly ConnectionFactory connectionFactory;
        private INativeConnection connection;
        
        public RabbitConnection(IEndpoint endpoint, string connectionString, IBusContext busContext, ILoggerFactory loggerFactory)
        {
            this.Id = Guid.NewGuid();
            this.endpoint = endpoint;
            this.ConnectionString = connectionString;
            this.busContext = busContext;
            this.loggerFactory = loggerFactory;
            this.logger = this.loggerFactory.CreateLogger<RabbitConnection>();
            var clientProperties = new Dictionary<string, object>
            {
                { "Endpoint", this.endpoint.Address },
                { "Machine", Environment.MachineName },
                { "Location", Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) },
                { "ConnectionId", this.Id.ToString() }
            };

            this.connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(this.ConnectionString),
                AutomaticRecoveryEnabled = false,
                ClientProperties = clientProperties,
                RequestedConnectionTimeout = ConnectionTimeout
            };
        }

        public event EventHandler Opened;

        public event EventHandler Closed;

        public event EventHandler Disposed;

        public Guid Id { get; }

        public string ConnectionString { get; }

        public void Open(CancellationToken token)
        {
            lock (this.syncRoot)
            {
                if (this.connection?.IsOpen ?? false)
                {
                    this.logger.LogTrace(
                        "Connection [{ConnectionId}] is already open",
                        this.Id);
                    return;
                }

                this.logger.LogInformation(
                    "Connecting to RabbitMQ using [{ConnectionString}]",
                    this.ConnectionString);
            
                var retryCount = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    INativeConnection con = null;
                    try
                    {
                        con = this.connectionFactory.CreateConnection();
                        con.ConnectionShutdown += this.OnConnectionShutdown;
                        this.connection = con;
                        this.OnOpened();
                        this.logger.LogInformation(
                            "Connection [{ConnectionId}] opened at [{Endpoint}]",
                            this.Id,
                            this.connection.Endpoint);

                        return;
                    }
                    catch (Exception ex)
                    {
                        var secondsToRetry = Math.Min(10, retryCount);

                        this.logger.LogWarning(
                            ex,
                            "Unable to connect to RabbitMQ. Retrying in {RetrySecond} seconds...",
                            secondsToRetry);

                        if (con != null)
                        {
                            con.ConnectionShutdown -= this.OnConnectionShutdown;
                            con.Abort(OperationTimeout);
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(secondsToRetry));
                        retryCount++;
                    }
                }
            }
        }

        [Obsolete("Use cancellable version")]
        public RabbitChannel OpenChannel()
        {
            lock (this.syncRoot)
            {
                if (this.connection == null || !this.connection.IsOpen)
                {
                    throw new InvalidOperationException("RabbitMQ connection is not open.");
                }

                try
                {
                    var model = this.connection.CreateModel();
                    var channel = new RabbitChannel(this.Id, model, this.busContext, this.loggerFactory);
                    return channel;
                }
                catch (Exception ex)
                {
                    this.logger.LogError(
                        ex,
                        "Failed to open a new channel in connection [{Connection}] due to: {Message}", 
                        this,
                        ex.Message);
                    throw;
                }
            }
        }

        public RabbitChannel OpenChannel(CancellationToken token)
        {
            lock (this.syncRoot)
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    if (this.connection == null || !this.connection.IsOpen)
                    {
                        this.Open(token);
                    }
                
                    try
                    {
                        var model = this.connection.CreateModel();
                        var channel = new RabbitChannel(this.Id, model, this.busContext, this.loggerFactory);
                        return channel;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(
                            ex,
                            "Failed to open a new channel due to: {Message}; retrying...", 
                            ex.Message);
                    }
                }
            }
        }

        public void Close()
        {
            lock (this.syncRoot)
            {
                if (this.connection != null)
                {
                    if (this.connection.CloseReason == null)
                    {
                        this.logger.LogTrace("[{Endpoint}]: closing connection.", this.endpoint);
                        try
                        {
                            this.connection.Close(OperationTimeout);
                            if (this.connection.CloseReason != null)
                            {
                                this.connection.Abort(OperationTimeout);
                            }
                        }
                        catch (AlreadyClosedException ex)
                        {
                            this.logger.LogWarning(
                                ex,
                                "[{Endpoint}]: connection is already closed: {Message}",
                                this.endpoint,
                                ex.Message);
                        }
                    }
                }
            }
        }

        public void Abort()
        {
            lock (this.syncRoot)
            {
                try
                {
                    this.connection?.Abort();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(
                        ex,
                        "[{Endpoint}]: failed to abort the underlying connection due to: {Message}",
                        this.endpoint,
                        ex.Message);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (this.syncRoot)
            {
                try
                {
                    this.Close();

                    if (this.connection != null)
                    {
                        this.logger.LogTrace(
                            "[{Endpoint}]: disposing connection [{ConnectionId}] at [{Endpoint}].",
                            this.endpoint,
                            this.Id,
                            this.connection.Endpoint);
                        this.connection?.Dispose();
                        this.connection = null;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(
                        ex,
                        "An error '{Message}' during connection cleanup has been suppressed",
                        ex.Message);
                }
                finally
                {
                    this.OnDisposed();
                }
            }
        }

        public override string ToString()
        {
            return $"{this.Id} : {this.ConnectionString}";
        }

        protected virtual void OnOpened()
        {
            this.Opened?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnClosed()
        {
            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDisposed()
        {
            this.Disposed?.Invoke(this, EventArgs.Empty);
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs eventArgs)
        {
            Task.Factory.StartNew(
                () =>
                {
                    this.logger.LogTrace(
                        "Connection [{ConnectionId}] has been closed due to {ReplyText} ({ReplyCode})",
                        this.Id,
                        eventArgs.ReplyText,
                        eventArgs.ReplyCode);

                    lock (this.syncRoot)
                    {
                        ((INativeConnection)sender).ConnectionShutdown -= this.OnConnectionShutdown;
                    }

                    this.OnClosed();
                });
        }
    }
}