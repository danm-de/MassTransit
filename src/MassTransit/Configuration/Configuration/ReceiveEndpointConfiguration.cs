﻿// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
namespace MassTransit.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Mime;
    using ConsumeConfigurators;
    using Context;
    using GreenPipes;
    using Pipeline;
    using Pipeline.Observables;
    using Saga;
    using SagaConfigurators;
    using Transports;


    public abstract class ReceiveEndpointConfiguration :
        IReceiveEndpointConfiguration
    {
        readonly IEndpointConfiguration _endpointConfiguration;
        readonly Lazy<IConsumePipe> _consumePipe;
        readonly IList<string> _lateConfigurationKeys;
        readonly IList<IReceiveEndpointSpecification> _specifications;
        IReceiveEndpoint _receiveEndpoint;

        protected ReceiveEndpointConfiguration(IEndpointConfiguration endpointConfiguration)
        {
            _endpointConfiguration = endpointConfiguration;

            _consumePipe = new Lazy<IConsumePipe>(() => _endpointConfiguration.Consume.CreatePipe());
            _specifications = new List<IReceiveEndpointSpecification>();
            _lateConfigurationKeys = new List<string>();

            EndpointObservers = new ReceiveEndpointObservable();
            ReceiveObservers = new ReceiveObservable();
            TransportObservers = new ReceiveTransportObservable();
        }

        public ReceiveEndpointObservable EndpointObservers { get; }
        public ReceiveObservable ReceiveObservers { get; }
        public ReceiveTransportObservable TransportObservers { get; }

        public IConsumePipeConfiguration Consume => _endpointConfiguration.Consume;
        public ISendPipeConfiguration Send => _endpointConfiguration.Send;
        public IPublishPipeConfiguration Publish => _endpointConfiguration.Publish;
        public IReceivePipeConfiguration Receive => _endpointConfiguration.Receive;

        public ITopologyConfiguration Topology => _endpointConfiguration.Topology;

        public ISerializationConfiguration Serialization => _endpointConfiguration.Serialization;

        public void AddPipeSpecification(IPipeSpecification<ConsumeContext> specification)
        {
            _endpointConfiguration.AddPipeSpecification(specification);
        }

        public ConnectHandle ConnectConsumerConfigurationObserver(IConsumerConfigurationObserver observer)
        {
            return _endpointConfiguration.ConnectConsumerConfigurationObserver(observer);
        }

        public ConnectHandle ConnectSagaConfigurationObserver(ISagaConfigurationObserver observer)
        {
            return _endpointConfiguration.ConnectSagaConfigurationObserver(observer);
        }

        public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
            where TConsumer : class
        {
            _endpointConfiguration.ConsumerConfigured(configurator);
        }

        public void ConsumerMessageConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
            where TConsumer : class
            where TMessage : class
        {
            _endpointConfiguration.ConsumerMessageConfigured(configurator);
        }

        public void SagaConfigured<TSaga>(ISagaConfigurator<TSaga> configurator)
            where TSaga : class, ISaga
        {
            _endpointConfiguration.SagaConfigured(configurator);
        }

        public void SagaMessageConfigured<TSaga, TMessage>(ISagaMessageConfigurator<TSaga, TMessage> configurator)
            where TSaga : class, ISaga
            where TMessage : class
        {
            _endpointConfiguration.SagaMessageConfigured(configurator);
        }

        public ConnectHandle ConnectHandlerConfigurationObserver(IHandlerConfigurationObserver observer)
        {
            return _endpointConfiguration.ConnectHandlerConfigurationObserver(observer);
        }

        public void HandlerConfigured<TMessage>(IHandlerConfigurator<TMessage> configurator)
            where TMessage : class
        {
            _endpointConfiguration.HandlerConfigured(configurator);
        }

        public ConnectHandle ConnectReceiveEndpointObserver(IReceiveEndpointObserver observer)
        {
            return EndpointObservers.Connect(observer);
        }

        public void AddPipeSpecification<T>(IPipeSpecification<ConsumeContext<T>> specification)
            where T : class
        {
            _endpointConfiguration.AddPipeSpecification(specification);
        }

        public void AddPrePipeSpecification(IPipeSpecification<ConsumeContext> specification)
        {
            _endpointConfiguration.AddPrePipeSpecification(specification);
        }

        public void ConfigureSend(Action<ISendPipeConfigurator> callback)
        {
            _endpointConfiguration.ConfigureSend(callback);
        }

        public void ConfigurePublish(Action<IPublishPipeConfigurator> callback)
        {
            _endpointConfiguration.ConfigurePublish(callback);
        }

        public void ConfigureReceive(Action<IReceivePipeConfigurator> callback)
        {
            _endpointConfiguration.ConfigureReceive(callback);
        }

        public void ConfigureDeadLetter(Action<IPipeConfigurator<ReceiveContext>> callback)
        {
            _endpointConfiguration.ConfigureDeadLetter(callback);
        }

        public void ConfigureError(Action<IPipeConfigurator<ExceptionReceiveContext>> callback)
        {
            _endpointConfiguration.ConfigureError(callback);
        }

        public virtual IEnumerable<ValidationResult> Validate()
        {
            return _endpointConfiguration.Validate()
                .Concat(_specifications.SelectMany(x => x.Validate()))
                .Concat(_lateConfigurationKeys.Select(x => this.Failure(x, "was modified after being used")));
        }

        public IConsumePipe ConsumePipe => _consumePipe.Value;

        public abstract Uri HostAddress { get; }
        public abstract Uri InputAddress { get; }

        public virtual IReceiveEndpoint ReceiveEndpoint
        {
            get
            {
                if (_receiveEndpoint == null)
                    throw new InvalidOperationException("The receive endpoint has not been built.");

                return _receiveEndpoint;
            }

            protected set => _receiveEndpoint = value;
        }

        public virtual IReceivePipe CreateReceivePipe()
        {
            return _endpointConfiguration.Receive.CreatePipe(ConsumePipe, _endpointConfiguration.Serialization.Deserializer);
        }

        protected virtual IReceiveEndpoint CreateReceiveEndpoint(string endpointName, IReceiveTransport receiveTransport,
            ReceiveEndpointContext receiveEndpointContext)
        {
            return new ReceiveEndpoint(receiveTransport, receiveEndpointContext);
        }

        public void SetMessageSerializer(SerializerFactory serializerFactory)
        {
            _endpointConfiguration.Serialization.SetSerializer(serializerFactory);
        }

        public void AddMessageDeserializer(ContentType contentType, DeserializerFactory deserializerFactory)
        {
            _endpointConfiguration.Serialization.AddDeserializer(contentType, deserializerFactory);
        }

        public void ClearMessageDeserializers()
        {
            _endpointConfiguration.Serialization.ClearDeserializers();
        }

        protected void ApplySpecifications(IReceiveEndpointBuilder builder)
        {
            for (var i = 0; i < _specifications.Count; i++)
                _specifications[i].Configure(builder);
        }

        public void AddEndpointSpecification(IReceiveEndpointSpecification specification)
        {
            _specifications.Add(specification);
        }

        protected void Changed(string key)
        {
            if (IsAlreadyConfigured())
                _lateConfigurationKeys.Add(key);
        }

        protected virtual bool IsAlreadyConfigured()
        {
            return false;
        }
    }
}
