using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using EPiServer.Events;
using EPiServer.Events.Providers;
using EPiServer.Logging;
using EPiServer.ServiceLocation;
using EPiServer.Web.Hosting;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace StefanOlsen.Events.Redis
{
    public class RedisEventProvider : EventProvider
    {
        private readonly ILogger _logger;
        private readonly EventsServiceKnownTypesLookup _knownTypesLookup;
        private IConnectionMultiplexer _connection;

        private string _applicationId;
        private string _machineName;
        private string _connectionString;
        private string _channelName;

        private JsonSerializerSettings _serializerSettings;
        private ISubscriber _subscriber;

        public RedisEventProvider() : this(
            LogManager.GetLogger(typeof(RedisEventProvider)),
            ServiceLocator.Current.GetInstance<EventsServiceKnownTypesLookup>())
        {
        }

        public RedisEventProvider(
            ILogger logger,
            EventsServiceKnownTypesLookup eventsServiceKnownTypesLookup)
        {
            _logger = logger;
            _knownTypesLookup = eventsServiceKnownTypesLookup;
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            _applicationId = GenericHostingEnvironment.Instance.ApplicationID;
            _machineName = Environment.MachineName;
            _channelName = config["channelName"];
            _connectionString = config["connectionString"];

            if (string.IsNullOrEmpty(_channelName))
            {
                throw new ConfigurationErrorsException("A ChannelName setting is missing.");
            }
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new ConfigurationErrorsException("A ConnectionString setting is missing.");
            }

            // The EventMessage object has a property called Parameters, of type object.
            // The JSON serializer/deserializer needs to know about possible value types for this property.
            Type[] knownTypes = _knownTypesLookup.KnownTypes.ToArray();
            _serializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Binder = new KnownTypesBinder(knownTypes)
            };
        }

        public override async Task InitializeAsync()
        {
            _logger.Information("Connecting to Redis.");
            _connection = await ConnectionMultiplexer.ConnectAsync(_connectionString);

            _logger.Information($"Setting up Redis subscription. Channel={_channelName}.");
            _subscriber = _connection.GetSubscriber();

            // Subscribe to the specific Redis channel.
            await _subscriber.SubscribeAsync(_channelName, OnMessageReceived);
        }

        public override void SendMessage(EventMessage message)
        {
            if (message == null)
            {
                return;
            }

            try
            {
                // Serialize the message to a JSON string.
                var json = JsonConvert.SerializeObject(message, _serializerSettings);
                _logger.Debug($"Sending event. EventId={message.EventId}. Sequence={message.SequenceNumber}.");

                // Publish the event to the Redis connection.
                _subscriber.Publish(_channelName, json);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to send event. EventId={message.EventId}. Sequence={message.SequenceNumber}.", ex);
            }
        }

        public override void Uninitialize()
        {
            base.Uninitialize();

            _logger.Information("Unsubscribing Redis event queue.");

            // Unsubscribe to the channel and dispose of the Redis connection.
            _subscriber.UnsubscribeAll();
            _connection.Dispose();
        }

        private void OnMessageReceived(RedisChannel channel, RedisValue value)
        {
            if (channel.IsNullOrEmpty ||
                value.IsNullOrEmpty)
            {
                return;
            }

            // Deserialize the JSON string to an EventMessage object.
            var message = JsonConvert.DeserializeObject<EventMessage>(value, _serializerSettings);
            _logger.Debug($"Received event. EventId={message.EventId}. Sequence={message.SequenceNumber}.");

            // If the message originated form ourself, ignore it.
            if (message.ServerName == _machineName &&
                message.ApplicationName == _applicationId)
            {
                _logger.Debug("Received event originated from this server itself.");
                return;
            }

            // Raise an event to let EPiServer know about this event.
            var eventArgs = new EventMessageEventArgs(message);
            OnMessageReceived(eventArgs);
        }
    }
}
