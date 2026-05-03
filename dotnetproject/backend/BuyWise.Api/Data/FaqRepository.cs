using BuyWise.Api.Models;

namespace BuyWise.Api.Data;

public interface IFaqRepository
{
    Task<IReadOnlyList<FaqDto>> GetAllAsync();
    Task<FaqDto?> FindBestMatchAsync(string message);
}

public sealed class FaqRepository : IFaqRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public FaqRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<FaqDto>> GetAllAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, question, answer, keywords FROM faqs ORDER BY question;";

        var faqs = new List<FaqDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            faqs.Add(new FaqDto(
                reader.GetInt32("id"),
                reader.GetString("question"),
                reader.GetString("answer"),
                reader.GetString("keywords")));
        }

        return faqs;
    }

    public async Task<FaqDto?> FindBestMatchAsync(string message)
    {
        var normalized = message.ToLowerInvariant();
        var faqs = await GetAllAsync();

        return faqs
            .Select(faq => new
            {
                Faq = faq,
                Score = faq.Keywords
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Count(keyword => normalized.Contains(keyword.ToLowerInvariant()))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Faq)
            .FirstOrDefault();
    }
}
