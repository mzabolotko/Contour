using Microsoft.Extensions.Logging;

namespace Contour.Receiving
{
    public interface IFailedDeliveryStrategyBuilder
    {
        IFailedDeliveryStrategy Build(ILoggerFactory loggerFactory);
    }
}
