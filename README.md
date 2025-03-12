using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http;

namespace OT.Assessment.Tester
{
    public class CasinoWagerEvent
    {
        public Guid WagerId { get; set; }
        public string Theme { get; set; }
        public string Provider { get; set; }
        public string GameName { get; set; }
        public Guid TransactionId { get; set; }
        public Guid BrandId { get; set; }
        public Guid AccountId { get; set; }
        public string Username { get; set; }
        public Guid ExternalReferenceId { get; set; }
        public Guid TransactionTypeId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int NumberOfBets { get; set; }
        public string CountryCode { get; set; }
        public string SessionData { get; set; }
        public long Duration { get; set; }
    }

    /// <summary>
    /// Example generator that picks random values from "tables" of possible data.
    /// If a table is empty, we fallback to a default value.
    /// </summary>
    public static class WagerEventGenerator
    {
        // Default fallback data if table is empty
        private static readonly List<string> DefaultThemes = new()
        {
            "adventure", "fantasy"
        };

        private static readonly List<string> DefaultProviders = new()
        {
            "Ergonomic Soft Fish", "MegaCasino"
        };

        private static readonly List<string> DefaultGameNames = new()
        {
            "Ergonomic Granite Cheese", "Epic Slot Machine"
        };

        private static readonly List<Guid> DefaultBrandIds = new()
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222")
        };

        private static readonly List<Guid> DefaultTransactionTypes = new()
        {
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444")
        };

        private static readonly List<Guid> DefaultAccounts = new()
        {
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666")
        };

        /// <summary>
        /// Generate a list of random CasinoWagerEvent using the provided tables or defaults.
        /// If a given table is null or empty, fallback to a default set.
        /// </summary>
        public static List<CasinoWagerEvent> Generate(
            int count = 100,
            List<string> themes = null,
            List<string> providers = null,
            List<string> gameNames = null,
            List<Guid> brandIds = null,
            List<Guid> transactionTypeIds = null,
            List<Guid> accountIds = null)
        {
            // If a table is empty, fallback to default
            themes ??= new List<string>();
            providers ??= new List<string>();
            gameNames ??= new List<string>();
            brandIds ??= new List<Guid>();
            transactionTypeIds ??= new List<Guid>();
            accountIds ??= new List<Guid>();

            if (!themes.Any()) themes = DefaultThemes;
            if (!providers.Any()) providers = DefaultProviders;
            if (!gameNames.Any()) gameNames = DefaultGameNames;
            if (!brandIds.Any()) brandIds = DefaultBrandIds;
            if (!transactionTypeIds.Any()) transactionTypeIds = DefaultTransactionTypes;
            if (!accountIds.Any()) accountIds = DefaultAccounts;

            var random = new Random();
            var events = new List<CasinoWagerEvent>(count);

            for (int i = 0; i < count; i++)
            {
                events.Add(new CasinoWagerEvent
                {
                    WagerId = Guid.NewGuid(),
                    Theme = themes[random.Next(themes.Count)],
                    Provider = providers[random.Next(providers.Count)],
                    GameName = gameNames[random.Next(gameNames.Count)],
                    TransactionId = Guid.NewGuid(),
                    BrandId = brandIds[random.Next(brandIds.Count)],
                    AccountId = accountIds[random.Next(accountIds.Count)],
                    Username = $"TestUser{i}",
                    ExternalReferenceId = Guid.NewGuid(),
                    TransactionTypeId = transactionTypeIds[random.Next(transactionTypeIds.Count)],
                    Amount = 100 + i,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    NumberOfBets = 3,
                    CountryCode = "BS",
                    SessionData = "Sample session data",  // stays hard-coded
                    Duration = 1000 + i
                });
            }

            return events;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            // Example: pass custom "tables" or use defaults
            var customThemes = new List<string> { "adventure", "mystery", "space" };
            var customProviders = new List<string> { "Ergonomic Soft Fish", "CoolGaming", "AnotherProvider" };
            var customGames = new List<string> { "Ergonomic Granite Cheese", "Ultra Slots", "Cosmic Poker" };

            // Possibly these could come from a DB or a file
            var testEvents = WagerEventGenerator.Generate(
                count: 100,
                themes: customThemes,
                providers: customProviders,
                gameNames: customGames
                // brandIds, transactionTypeIds, accountIds can also be passed
            );

            // Now define your NBomber Scenarios using the generated testEvents
            var postScenario = Scenario.Create("post_casinowager", async context =>
            {
                int index = (int)(context.InvocationNumber % testEvents.Count);
                var payload = testEvents[index];
                string body = JsonSerializer.Serialize(payload);

                using var httpClient = new HttpClient();

                var request = Http.CreateRequest("POST", "http://localhost:7120/api/player/casinowager")
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent(body, Encoding.UTF8, "application/json"));

                var response = await Http.Send(httpClient, request);

                return response.StatusCode == "OK"
                    ? Response.Ok<object>(null, "OK", 0, "")
                    : Response.Fail(body, response.StatusCode, response.Message, response.SizeBytes);
            })
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.IterationsForInject(rate: 500, interval: TimeSpan.FromSeconds(2), iterations: 7000)
            );

            var getWagersScenario = Scenario.Create("get_wagers", async context =>
            {
                var playerId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync($"http://localhost:7120/api/player/{playerId}/wagers?pageSize=10&page=1");
                var content = await response.Content.ReadAsStringAsync();

                return response.IsSuccessStatusCode
                    ? Response.Ok<string>(content, "OK", content.Length, "")
                    : Response.Fail(content, response.StatusCode.ToString(), "GET wagers failed", content.Length);
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(30))
            );

            var getTopSpendersScenario = Scenario.Create("get_top_spenders", async context =>
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync("http://localhost:7120/api/player/topSpenders?count=10");
                var content = await response.Content.ReadAsStringAsync();

                return response.IsSuccessStatusCode
                    ? Response.Ok<string>(content, "OK", content.Length, "")
                    : Response.Fail(content, response.StatusCode.ToString(), "GET top spenders failed", content.Length);
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(30))
            );

            NBomberRunner
                .RegisterScenarios(postScenario, getWagersScenario, getTopSpendersScenario)
                .WithoutReports()
                .Run();

            Console.WriteLine("Load test completed. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
