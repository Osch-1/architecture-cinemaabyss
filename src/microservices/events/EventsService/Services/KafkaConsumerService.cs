using Confluent.Kafka;

namespace EventsService.Services;

class KafkaConsumerService( ILogger<KafkaConsumerService> logger, string kafkaBrokers ) : BackgroundService
{
    private readonly string[] _topics = [ "movie-events", "user-events", "payment-events" ];

    protected override Task ExecuteAsync( CancellationToken ct ) =>
        Task.Run( () => Consume( ct ), ct );

    private void Consume( CancellationToken ct )
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaBrokers,
            GroupId          = "events-service-consumer",
            AutoOffsetReset  = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>( config ).Build();
        consumer.Subscribe( _topics );

        logger.LogInformation( "Kafka consumer subscribed to: {Topics}", string.Join( ", ", _topics ) );

        while ( !ct.IsCancellationRequested )
        {
            try
            {
                var result = consumer.Consume( ct );
                logger.LogInformation(
                    "[{Topic}] Consumed | partition={Partition} offset={Offset} | key={Key} value={Value}",
                    result.Topic, result.Partition.Value, result.Offset.Value,
                    result.Message.Key, result.Message.Value );
            }
            catch ( OperationCanceledException ) { break; }
            catch ( ConsumeException ex )
            {
                logger.LogError( ex, "Kafka consume error" );
            }
        }

        consumer.Close();
        logger.LogInformation( "Kafka consumer stopped" );
    }
}
