using System.Net;
using Statikk_Scraper.Models.Enums;

namespace Statikk_Scraper.Data.Models;

public class Audits
{
    public ulong Id { get; init; }
    public string Method { get; init; }
    public string Input { get; init; }
    public string Message { get; init; }
    public string StackTrace { get; init; }
    public Status Status { get; init; }
    public IPAddress IPAddress { get; init; }
    public DateTime Timestamp { get; init; }
}