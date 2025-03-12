using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using OT.Assessment.App;
using OT.Assessment.App.Data;
using OT.Assessment.App.Services;
using OT.Assessment.Consumer;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureLogging(logging =>
    {
        // Clear other logging providers and add Console logging.
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        // Register the DbContext with SQL Server connection.
        services.AddDbContext<OTAssessmentDbContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("DatabaseConnection")));

        // Bind RabbitMQ configuration from appsettings.json.
        var rabbitMqOptions = context.Configuration.GetSection("RabbitMqConfig").Get<RabbitMqConfig>();

        services.AddSingleton<IModel>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = rabbitMqOptions.Host,
                Port = rabbitMqOptions.Port,
                UserName = rabbitMqOptions.UserName,
                Password = rabbitMqOptions.Password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(30000),
                Ssl = { Enabled = false },
                VirtualHost = rabbitMqOptions.VirtualHost
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            channel.QueueDeclare(
                queue: "CasinoWagerQueue", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                arguments: null);
            return channel;
        });

        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        services.AddHostedService<CasinoWagerConsumerService>();
    })
    .Build();

await host.RunAsync();
