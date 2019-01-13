using Microsoft.Extensions.Logging;

namespace Contour.Common.Tests
{
    internal class FakeConnectionPool : ConnectionPool<IConnection>
    {
        public FakeConnectionPool(IConnectionProvider<IConnection> provider, ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            this.Provider = provider;
        }
    }
}