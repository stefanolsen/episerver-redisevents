# Redis Event Provider for EPiServer
This projects contains a special EPiServer Event Provider, developed to be using a Publish/Subscribe channel on a [Redis](https://redis.io/) instance.

## Installation
Download and reference the latest binary file [here](https://github.com/stefanolsen/episerver-redisevents/releases).

Update the web.config file in your web project to reflect this example. In case the `<event>` element does not exist, you need to add it.
```
<event defaultProvider="RedisEventProvider">
  <providers>
    <add name="RedisEventProvider" type="StefanOlsen.Events.Redis.RedisEventProvider, StefanOlsen.Events.Redis" connectionString="localhost" channelName="EPiServerEvents"/>
  </providers>
</event>
```

Insert your own connection string value in the `connectionString` attribute ([look here for options](https://stackexchange.github.io/StackExchange.Redis/Configuration)).