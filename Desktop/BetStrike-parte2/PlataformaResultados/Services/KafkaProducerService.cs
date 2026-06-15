using Confluent.Kafka;
using Newtonsoft.Json;

namespace PlataformaResultados.Services
{
    /// <summary>
    /// Publica eventos de jogos no Kafka a partir da PlataformaResultados.
    /// Permite que outros consumidores (ex. analytics) recebam atualizações
    /// diretamente da fonte, sem passar pela BetStrikeAPI.
    /// </summary>
    public class KafkaProducerService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        public const string TOPIC_GAME_EVENTS = "game-events";

        public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;
            var servers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
            _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers      = servers,
                Acks                  = Acks.Leader,
                MessageSendMaxRetries = 3,
                RetryBackoffMs        = 1000
            }).Build();
            _logger.LogInformation("[KAFKA Resultados] Inicializado. Bootstrap: {S}", servers);
        }

        public async Task PublicarEvento(string codigoJogo, string tipoEvento, int estado,
            int? golosCasa, int? golosFora, string equipaCasa, string equipaFora, string competicao)
        {
            try
            {
                var ev = new
                {
                    CodigoJogo = codigoJogo,
                    TipoEvento = tipoEvento,
                    Estado     = estado,
                    GolosCasa  = golosCasa,
                    GolosFora  = golosFora,
                    EquipaCasa = equipaCasa,
                    EquipaFora = equipaFora,
                    Competicao = competicao,
                    Timestamp  = DateTime.UtcNow
                };
                var json = JsonConvert.SerializeObject(ev);
                await _producer.ProduceAsync(TOPIC_GAME_EVENTS,
                    new Message<string, string> { Key = codigoJogo, Value = json });
            }
            catch (Exception ex)
            {
                _logger.LogError("[KAFKA Resultados ERRO] {Msg}", ex.Message);
            }
        }

        public void Dispose() => _producer?.Dispose();
    }
}
