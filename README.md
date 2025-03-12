using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OT.Assessment.App.Data;
using OT.Assessment.App.Models;

using RModel = RabbitMQ.Client.IModel;

namespace OT.Assessment.Consumer
{
    public class CasinoWagerConsumerService : BackgroundService
    {
        private readonly ILogger<CasinoWagerConsumerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private IConnection _connection;
        private RModel _channel;

        private const string QueueName = "CasinoWagerQueue";

        private static readonly ConcurrentQueue<CasinoWager> _wagerQueue = new();
        private static readonly SemaphoreSlim _queueLock = new(1, 1);

        public CasinoWagerConsumerService(IServiceScopeFactory scopeFactory, ILogger<CasinoWagerConsumerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            InitializeRabbitMqListener();
        }

        private void InitializeRabbitMqListener()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 50, global: false);

            _logger.LogInformation("Connected to RabbitMQ. Listening on queue: {QueueName}", QueueName);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("CasinoWagerConsumerService is stopping.");
            });

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (sender, eventArgs) =>
            {
                Task.Run(async () =>
                {
                    var body = eventArgs.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received message: {Message}", message);

                    try
                    {
                        var wagerEvent = JsonSerializer.Deserialize<CasinoWagerEvent>(message);
                        if (wagerEvent != null)
                        {
                            var wager = new CasinoWager
                            {
                                WagerId = wagerEvent.WagerId,
                                AccountId = wagerEvent.AccountId,
                                Username = wagerEvent.Username,
                                GameName = wagerEvent.GameName,
                                Provider = wagerEvent.Provider,
                                Amount = wagerEvent.Amount,
                                CreatedDateTime = wagerEvent.CreatedDateTime
                            };

                            _wagerQueue.Enqueue(wager);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message: {Message}", message);
                    }
                    finally
                    {
                        _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                    }
                });
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            _ = Task.Run(ProcessQueue);
            return Task.CompletedTask;
        }

        private async Task ProcessQueue()
        {
            while (true)
            {
                if (_wagerQueue.Count >= 100)
                {
                    await _queueLock.WaitAsync();
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<OTAssessmentDbContext>();

                        var batch = new List<CasinoWager>();
                        while (_wagerQueue.TryDequeue(out var wager) && batch.Count < 100)
                        {
                            batch.Add(wager);
                        }

                        if (batch.Count > 0)
                        {
                            await dbContext.CasinoWagers.AddRangeAsync(batch);
                            await dbContext.SaveChangesAsync();
                            _logger.LogInformation("Batch inserted {Count} messages", batch.Count);
                        }
                    }
                    finally
                    {
                        _queueLock.Release();
                    }
                }

                await Task.Delay(100);
            }
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
