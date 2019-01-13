using Microsoft.Extensions.Logging;
using System;

namespace Contour.Receiving
{

    /// <summary>
    /// The lambda failed delivery strategy.
    /// </summary>
    public class LambdaFailedDeliveryStrategy : IFailedDeliveryStrategy
    {
        /// <summary>
        /// The _handler action.
        /// </summary>
        private readonly Action<IFailedConsumingContext> _handlerAction;
        private readonly ILogger<LambdaFailedDeliveryStrategy> logger;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="LambdaFailedDeliveryStrategy"/>.
        /// </summary>
        /// <param name="handlerAction">
        /// The handler action.
        /// </param>
        public LambdaFailedDeliveryStrategy(Action<IFailedConsumingContext> handlerAction, ILogger<LambdaFailedDeliveryStrategy> logger)
        {
            this._handlerAction = handlerAction;
            this.logger = logger;
        }

        /// <summary>
        /// The handle.
        /// </summary>
        /// <param name="failedConsumingContext">
        /// The failed consuming context.
        /// </param>
        public void Handle(IFailedConsumingContext failedConsumingContext)
        {
            try
            {
                this._handlerAction(failedConsumingContext);
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Unable to handle failed message [{Label}].", 
                    failedConsumingContext.Delivery.Label);
            }
        }

    }
}
