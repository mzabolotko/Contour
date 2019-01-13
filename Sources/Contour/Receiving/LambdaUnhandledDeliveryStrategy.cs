using Microsoft.Extensions.Logging;
using System;

namespace Contour.Receiving
{
    /// <summary>
    /// The lambda unhandled delivery strategy.
    /// </summary>
    public class LambdaUnhandledDeliveryStrategy : IUnhandledDeliveryStrategy
    {
        /// <summary>
        /// The _handler action.
        /// </summary>
        private readonly Action<IUnhandledConsumingContext> handlerAction;
        private readonly ILogger<LambdaUnhandledDeliveryStrategy> logger;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="LambdaUnhandledDeliveryStrategy"/>.
        /// </summary>
        /// <param name="handlerAction">
        /// The handler action.
        /// </param>
        public LambdaUnhandledDeliveryStrategy(Action<IUnhandledConsumingContext> handlerAction, ILogger<LambdaUnhandledDeliveryStrategy> logger)
        {
            this.handlerAction = handlerAction;
            this.logger = logger;
        }

        /// <summary>
        /// The handle.
        /// </summary>
        /// <param name="unhandledConsumingContext">
        /// The unhandled consuming context.
        /// </param>
        public void Handle(IUnhandledConsumingContext unhandledConsumingContext)
        {
            try
            {
                this.handlerAction(unhandledConsumingContext);
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Unable to handle failed message [{Label}].", 
                    unhandledConsumingContext.Delivery.Label);
            }
        }
    }
}
