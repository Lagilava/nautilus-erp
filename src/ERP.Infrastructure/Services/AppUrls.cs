using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Services;

/// <summary>Reads public front-end URLs from configuration ("App:ClientBaseUrl").</summary>
public sealed class AppUrls : IAppUrls
{
    public AppUrls(IConfiguration configuration)
        => ClientBaseUrl = (configuration["App:ClientBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');

    public string ClientBaseUrl { get; }
}
