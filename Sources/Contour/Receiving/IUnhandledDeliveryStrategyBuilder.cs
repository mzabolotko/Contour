using Microsoft.Extensions.Logging;

namespace Contour.Receiving
{
    public interface IUnhandledDeliveryStrategyBuilder
    {
        IUnhandledDeliveryStrategy Build(ILoggerFactory loggerFactory);
    }
}
