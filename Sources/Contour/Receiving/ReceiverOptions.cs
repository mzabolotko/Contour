using System;

using Contour.Configuration;
using Contour.Helpers;

namespace Contour.Receiving
{
    /// <summary>
    /// Настройки получателя.
    /// </summary>
    public class ReceiverOptions : EndpointOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReceiverOptions"/> class. 
        /// </summary>
        public ReceiverOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReceiverOptions"/> class. 
        /// </summary>
        /// <param name="parent">
        /// Базовые настройки.
        /// </param>
        public ReceiverOptions(BusOptions parent)
            : base(parent)
        {
        }

        /// <summary>
        /// Устанавливает необходимость явного подтверждения успешной обработки.
        /// </summary>
        public Maybe<bool> AcceptIsRequired { protected get; set; }

        /// <summary>
        /// Построитель порта получения входящих сообщений.
        /// </summary>
        public Maybe<Func<ISubscriptionEndpointBuilder, ISubscriptionEndpoint>> EndpointBuilder { protected get; set; }

        /// <summary>
        /// Количество одновременных обработчиков сообщений.
        /// </summary>
        public Maybe<uint> ParallelismLevel { protected get; set; }

        /// <summary>
        /// Длительность хранения сообщений в Fault очереди.
        /// </summary>
        public Maybe<TimeSpan> FaultQueueTtl { protected get; set; }

        /// <summary>
        /// Максимальное количество сообщений в Fault очереди.
        /// </summary>
        public Maybe<int> FaultQueueLimit { protected get; set; }

         /// <summary>
        /// Создает новый экземпляр настроек как копию существующего.
        /// </summary>
        /// <returns>
        /// Новый экземпляр настроек.
        /// </returns>
        public override BusOptions Derive()
        {
            return new ReceiverOptions(this);
        }

        /// <summary>
        /// Возвращает построитель порта получаемых сообщений.
        /// </summary>
        /// <returns>
        /// Построитель порта получаемых сообщений.
        /// </returns>
        public Maybe<Func<ISubscriptionEndpointBuilder, ISubscriptionEndpoint>> GetEndpointBuilder()
        {
            return this.Pick<ReceiverOptions, Func<ISubscriptionEndpointBuilder, ISubscriptionEndpoint>>((o) => o.EndpointBuilder);
        }

        /// <summary>
        /// Возвращает количество одновременных обработчиков сообщений.
        /// </summary>
        /// <returns>
        /// Количество одновременных обработчиков сообщений.
        /// </returns>
        public Maybe<uint> GetParallelismLevel()
        {
            return this.Pick<ReceiverOptions, uint>((o) => o.ParallelismLevel);
        }

         /// <summary>
        /// Возвращает признак необходимости явно подтверждать успешно обработанные сообщения.
        /// </summary>
        /// <returns>
        /// Если <c>true</c> - тогда необходимо подтверждать успешно обработанные сообщения, иначе - <c>false</c>.
        /// </returns>
        public Maybe<bool> IsAcceptRequired()
        {
            return this.Pick<ReceiverOptions, bool>((o) => o.AcceptIsRequired);
        }

        /// <summary>
        /// Возвращает длительность хранения сообщений в Fault очереди.
        /// </summary>
        /// <returns>
        /// Возвращает длительность хранения сообщений в Fault очереди.
        /// </returns>
        public Maybe<TimeSpan> GetFaultQueueTtl()
        {
            return this.Pick<ReceiverOptions, TimeSpan>((o) => o.FaultQueueTtl);
        }

        /// <summary>
        /// Возвращает максимальное количество сообщений в Fault очереди.
        /// </summary>
        /// <returns>
        /// Возвращает максимальное количество сообщений в Fault очереди.
        /// </returns>
        public Maybe<int> GetFaultQueueLimit()
        {
            return this.Pick<ReceiverOptions, int>((o) => o.FaultQueueLimit);
        }

        public IUnhandledDeliveryStrategyBuilder UnhandledDeliveryStrategyBuilder { protected get; set; }

        public IUnhandledDeliveryStrategyBuilder GetUnhandledDeliveryStrategyBuilder()
        {
            return this.Pick<ReceiverOptions, IUnhandledDeliveryStrategyBuilder>((o) => o.UnhandledDeliveryStrategyBuilder);
        }

        public IFailedDeliveryStrategyBuilder FailedDeliveryStrategyBuilder { protected get; set; }

        public IFailedDeliveryStrategyBuilder GetFailedDeliveryStrategyBuilder()
        {
            return this.Pick<ReceiverOptions, IFailedDeliveryStrategyBuilder>((o) => o.FailedDeliveryStrategyBuilder);
        }
    }
}
